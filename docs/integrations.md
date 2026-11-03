# Integrations

This document describes how NuciAPI.Middleware integrates with common frameworks, tools, and platforms.

## ASP.NET Core Integration

### Middleware Pipeline

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Required for IP extraction
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));
});

var app = builder.Build();

app.UseForwardedHeaders();              // 1st: Process proxy headers
app.UseMiddleware<LoggingMiddleware>(); // 2nd: Logging
app.UseMiddleware<AuthMiddleware>();    // 3rd: Your NuciApiMiddleware-derived class
app.MapControllers();

app.Run();
```

### Dependency Injection

```csharp
// Register consumer services
builder.Services.AddSingleton<IHmacValidator, HmacValidator>();
builder.Services.AddSingleton<IClientRegistry, SqlClientRegistry>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// Library requires NO registration — just inherits RequestDelegate
```

### Minimal APIs

```csharp
// Works with Minimal APIs
var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<AuthMiddleware>();

app.MapGet("/api/data", (HttpContext ctx) => 
{
    var clientId = ctx.Request.Headers["X-Client-ID"].FirstOrDefault();
    return Results.Ok(new { ClientId = clientId });
});

app.Run();
```

## Container Integration

### Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuciAPI.Middleware.sln", "."]
COPY ["NuciAPI.Middleware/NuciAPI.Middleware.csproj", "NuciAPI.Middleware/"]
COPY ["NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj", "NuciAPI.Middleware.UnitTests/"]
RUN dotnet restore "NuciAPI.Middleware.sln"

COPY . .
WORKDIR "/src/NuciAPI.Middleware"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Kubernetes

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: my-api
  template:
    metadata:
      labels:
        app: my-api
    spec:
      containers:
      - name: my-api
        image: my-registry/my-api:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        - name: ForwardedHeaders__KnownProxies__0
          value: "10.0.0.0/8"  # Cluster CIDR
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
```

## Cloud Provider Integration

### Azure App Service

```csharp
// Program.cs - Azure-specific forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Azure uses private IPs for front-end proxies
    options.KnownNetworks.Clear();
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
});
```

### AWS Elastic Load Balancer

```csharp
// ALB sends X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Port
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // AWS ALB IPs vary by region — use KnownNetworks for VPC CIDR
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
});
```

### Google Cloud Run

```csharp
// Cloud Run sets X-Forwarded-For automatically
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Cloud Run uses GFE proxies
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("35.191.0.0"), 16));
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("130.211.0.0"), 16));
});
```

## Observability Integration

### OpenTelemetry

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("NuciAPI.Middleware")
        .AddOtlpExporter());

// Custom middleware with tracing
public sealed class TracedMiddleware(RequestDelegate next, ILogger<TracedMiddleware> logger) 
    : NuciApiMiddleware(next)
{
    private static readonly ActivitySource ActivitySource = new("NuciAPI.Middleware");
    
    public override async Task InvokeAsync(HttpContext context)
    {
        using var activity = ActivitySource.StartActivity("Middleware.Invoke", ActivityKind.Server);
        activity?.SetTag("client.id", TryGetHeaderValue(context.Request, NuciApiHeaderNames.ClientId) ?? "unknown");
        activity?.SetTag("client.ip", GetClientIpAddress(context) ?? "unknown");
        
        await Next(context);
        
        activity?.SetTag("http.status_code", context.Response.StatusCode);
    }
}
```

### Prometheus / Grafana

```csharp
// Metrics middleware
public sealed class MetricsMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    private static readonly Counter RequestCounter = Metrics
        .CreateCounter("nuciapi_middleware_requests_total", "Total requests", "client_id", "status");
    private static readonly Histogram RequestDuration = Metrics
        .CreateHistogram("nuciapi_middleware_request_duration_seconds", "Request duration", "client_id");
    
    public override async Task InvokeAsync(HttpContext context)
    {
        var clientId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.ClientId) ?? "unknown";
        var sw = Stopwatch.StartNew();
        
        try
        {
            await Next(context);
            RequestCounter.WithLabels(clientId, context.Response.StatusCode.ToString()).Inc();
        }
        catch
        {
            RequestCounter.WithLabels(clientId, "error").Inc();
            throw;
        }
        finally
        {
            RequestDuration.WithLabels(clientId).Observe(sw.Elapsed.TotalSeconds);
        }
    }
}
```

### Serilog

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MyApi")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq("http://seq:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// In middleware
public sealed class LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger) 
    : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId) ?? Guid.NewGuid().ToString("N");
        
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("ClientId", TryGetHeaderValue(context.Request, NuciApiHeaderNames.ClientId) ?? "anonymous"))
        using (LogContext.PushProperty("ClientIp", GetClientIpAddress(context) ?? "unknown"))
        {
            logger.LogInformation("Request started");
            await Next(context);
            logger.LogInformation("Request completed with status {StatusCode}", context.Response.StatusCode);
        }
    }
}
```

## CI/CD Integration

### GitHub Actions (Already Configured)

```yaml
# .github/workflows/dotnet.yml
- Build and test on push/PR to master
- Runs: restore, build, test, format check

# .github/workflows/github-release.yml
- On GitHub Release: pack and publish to NuGet.org
```

### Azure DevOps

```yaml
# azure-pipelines.yml
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

steps:
- task: DotNetCoreCLI@2
  inputs:
    command: 'restore'
    projects: 'NuciAPI.Middleware.sln'

- task: DotNetCoreCLI@2
  inputs:
    command: 'build'
    projects: 'NuciAPI.Middleware.sln'
    arguments: '--configuration $(buildConfiguration)'

- task: DotNetCoreCLI@2
  inputs:
    command: 'test'
    projects: 'NuciAPI.Middleware.sln'
    arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage"'

- task: DotNetCoreCLI@2
  inputs:
    command: 'pack'
    projects: 'NuciAPI.Middleware/NuciAPI.Middleware.csproj'
    arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)'

- task: NuGetCommand@2
  inputs:
    command: 'push'
    packagesToPush: '$(Build.ArtifactStagingDirectory)/*.nupkg'
    nuGetFeedType: 'external'
    publishFeedCredentials: 'NuGet.org'
```

## Testing Framework Integration

### xUnit (Alternative to NUnit)

```xml
<!-- NuciAPI.Middleware.UnitTests.csproj -->
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
<PackageReference Include="Moq" Version="4.20.72" />
```

### NSubstitute (Alternative to Moq)

```xml
<PackageReference Include="NSubstitute" Version="5.1.0" />
```

```csharp
// Test with NSubstitute
var validator = Substitute.For<IHmacValidator>();
validator.Validate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<HttpRequest>(), Arg.Any<string>())
    .Returns(true);
```

## IDE Integration

### Visual Studio

- Full IntelliSense support
- Go to Definition (SourceLink enabled)
- NuGet package manager integration
- Test Explorer for NUnit/xUnit

### VS Code

```json
// .vscode/extensions.json
{
  "recommendations": [
    "ms-dotnettools.csharp",
    "ms-dotnettools.vscode-dotnet-runtime",
    "formatters.dotnet-format"
  ]
}
```

### Rider

- Full support for .NET 10
- Built-in test runner
- NuGet integration

## Related Documents

- [Build and Deployment](build-and-deployment.md) — CI/CD details
- [Components: Host and Composition](components/host-and-composition.md) — Pipeline integration
- [Testing](testing.md) — Test framework setup
- [Troubleshooting](troubleshooting.md) — Common integration issues