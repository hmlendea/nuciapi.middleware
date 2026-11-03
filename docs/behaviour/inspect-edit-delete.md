# Inspect and Modify Behavior

This document describes how consumers inspect and customize NuciAPI.Middleware behavior.

## Inspecting Runtime Behavior

### Header Values

```csharp
// In your derived middleware
public override async Task InvokeAsync(HttpContext context)
{
    // Inspect all standard headers
    var clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
    var timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
    var hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
    
    // Log for debugging
    _logger.LogDebug("Headers: ClientId={ClientId}, RequestId={RequestId}, Timestamp={Timestamp}", 
        clientId, requestId ?? "none", timestamp);
    
    await Next(context);
}
```

### Client IP

```csharp
var clientIp = GetClientIpAddress(context);
_logger.LogInformation("Client IP: {ClientIp}", clientIp ?? "unknown");

// Inspect proxy headers manually if needed
var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
var forwarded = context.Request.Headers["Forwarded"].FirstOrDefault();
```

### Hostnames

```csharp
var hostnames = GetClientHostname(context);
_logger.LogDebug("Resolved hostnames: {Hostnames}", string.Join(", ", hostnames));
```

## Modifying Behavior

### Override IP Extraction

```csharp
public sealed class CustomIpMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    protected override string GetClientIpAddress(HttpContext context)
    {
        // 1. Check custom header first
        if (context.Request.Headers.TryGetValue("X-Custom-Client-IP", out var customIp))
        {
            if (IPAddress.TryParse(customIp, out var parsed))
                return parsed.ToString();
        }
        
        // 2. Fall back to base implementation
        return base.GetClientIpAddress(context);
    }
    
    public override async Task InvokeAsync(HttpContext context)
    {
        var ip = GetClientIpAddress(context);
        // Use IP for geo-blocking, rate limiting, etc.
        await Next(context);
    }
}
```

### Override Hostname Resolution

```csharp
public sealed class CachedHostnameMiddleware(RequestDelegate next, IDnsCache cache) 
    : NuciApiMiddleware(next)
{
    private readonly IDnsCache _cache = cache;
    
    protected override List<string> GetClientHostname(HttpContext context)
    {
        var ip = GetClientIpAddress(context);
        if (ip == null) return [];
        
        // Check cache first
        if (_cache.TryGet(ip, out var cached))
            return cached;
        
        // Resolve and cache
        var hostnames = base.GetClientHostname(context);
        _cache.Set(ip, hostnames, TimeSpan.FromMinutes(5));
        return hostnames;
    }
    
    public override async Task InvokeAsync(HttpContext context)
    {
        await Next(context);
    }
}
```

### Custom Header Validation

```csharp
public sealed class ValidatingMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        // Custom validation before base processing
        var clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        
        if (!IsValidClientIdFormat(clientId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid ClientId format");
            return;
        }
        
        // Custom timestamp validation
        var timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
        if (!IsTimestampFresh(timestamp, TimeSpan.FromMinutes(2))) // Stricter than default
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Timestamp too old");
            return;
        }
        
        await Next(context);
    }
    
    private bool IsValidClientIdFormat(string clientId)
        => !string.IsNullOrWhiteSpace(clientId) && clientId.Length <= 64;
    
    private bool IsTimestampFresh(string timestamp, TimeSpan maxAge)
    {
        if (!DateTimeOffset.TryParse(timestamp, out var requestTime))
            return false;
        return (DateTimeOffset.UtcNow - requestTime) <= maxAge;
    }
}
```

### Add Custom Headers to Response

```csharp
public sealed class ResponseHeaderMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId) 
            ?? Guid.NewGuid().ToString("N");
        
        // Add correlation ID to response
        context.Response.Headers["X-Request-ID"] = requestId;
        context.Response.Headers["X-Response-Time"] = "0"; // Will update after
        
        var stopwatch = Stopwatch.StartNew();
        await Next(context);
        stopwatch.Stop();
        
        context.Response.Headers["X-Response-Time"] = stopwatch.ElapsedMilliseconds.ToString();
    }
}
```

## Configuration-Driven Behavior

### Using Options Pattern

```csharp
// MyMiddlewareOptions.cs
public sealed class MyMiddlewareOptions
{
    public TimeSpan TimestampTolerance { get; set; } = TimeSpan.FromMinutes(5);
    public string[] TrustedProxyHeaders { get; set; } = ["X-Forwarded-For", "Forwarded"];
    public bool RequireHmac { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 100;
}

// Registration
builder.Services.Configure<MyMiddlewareOptions>(builder.Configuration.GetSection("MyMiddleware"));

// Usage in middleware
public sealed class ConfigurableMiddleware(
    RequestDelegate next, 
    IOptions<MyMiddlewareOptions> options) : NuciApiMiddleware(next)
{
    private readonly MyMiddlewareOptions _options = options.Value;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        if (_options.RequireHmac)
        {
            var hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
            // Validate...
        }
        
        // Use _options.TimestampTolerance, etc.
        await Next(context);
    }
}
```

## Testing Modified Behavior

### Unit Test Custom IP Extraction

```csharp
[Test]
public void Given_CustomHeader_When_GetClientIpAddress_Then_ReturnsCustomIp()
{
    // Arrange
    var middleware = new TestCustomIpMiddleware(_ => Task.CompletedTask);
    var context = new DefaultHttpContext();
    context.Request.Headers["X-Custom-Client-IP"] = "203.0.113.50";
    
    // Act
    var ip = middleware.GetClientIpAddress(context);
    
    // Assert
    Assert.That(ip, Is.EqualTo("203.0.113.50"));
}

[Test]
public void Given_NoCustomHeader_When_GetClientIpAddress_Then_FallsBackToBase()
{
    // Arrange
    var middleware = new TestCustomIpMiddleware(_ => Task.CompletedTask);
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.10");
    
    // Act
    var ip = middleware.GetClientIpAddress(context);
    
    // Assert
    Assert.That(ip, Is.EqualTo("198.51.100.10"));
}
```

### Integration Test with TestServer

```csharp
[Test]
public async Task Given_CustomMiddleware_When_Request_Then_CustomBehaviorExecutes()
{
    // Arrange
    var builder = new WebHostBuilder()
        .ConfigureServices(s => s.AddSingleton<IHmacValidator, TestValidator>())
        .Configure(app => 
        {
            app.UseForwardedHeaders();
            app.UseMiddleware<CustomIpMiddleware>();
            app.UseMiddleware<ValidatingMiddleware>();
            app.Run(ctx => ctx.Response.WriteAsync("OK"));
        });
    
    var server = new TestServer(builder);
    var client = server.CreateClient();
    client.DefaultRequestHeaders.Add("X-Custom-Client-IP", "203.0.113.50");
    client.DefaultRequestHeaders.Add("X-Client-ID", "test-client");
    
    // Act
    var response = await client.GetAsync("/");
    
    // Assert
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

## Related Documents

- [Components: Application Services](../components/application-services.md) — Helper method contracts
- [Flows: Client IP Extraction](../flows/client-ip-extraction.md) — Base IP extraction logic
- [API Usage Examples](../api-usage-examples.md) — Complete examples
- [Testing](../testing.md) — Test strategies