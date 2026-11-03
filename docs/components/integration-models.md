# Integration Models

This document describes how NuciAPI.Middleware integrates with external systems and the models used for integration.

## Integration Patterns

### 1. Middleware Pipeline Integration

NuciAPI.Middleware integrates into the ASP.NET Core pipeline as a standard middleware component.

```csharp
// Program.cs
var app = builder.Build();

app.UseForwardedHeaders();           // Required for IP extraction
app.UseMiddleware<LoggingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<AuthMiddleware>(); // Your NuciApiMiddleware-derived class
app.MapControllers();

app.Run();
```

**Integration Points:**
- `RequestDelegate next` — Next middleware in pipeline
- `HttpContext` — Full request/response access
- `IServiceProvider` — DI for consumer services

### 2. Dependency Injection Integration

Consumers register their services; the library has no DI requirements.

```csharp
// Consumer services
builder.Services.AddSingleton<IHmacValidator, HmacValidator>();
builder.Services.AddSingleton<IClientRegistry, SqlClientRegistry>();
builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// Library requires nothing — just inherits RequestDelegate
```

### 3. Configuration Integration

No library configuration. Consumers bind their own:

```csharp
// appsettings.json
{
  "Auth": {
    "TimestampToleranceMinutes": 5,
    "RequireHmac": true,
    "AllowedClockSkewSeconds": 30
  },
  "RateLimiting": {
    "DefaultLimit": 100,
    "WindowMinutes": 1
  }
}

// Binding
builder.Services.Configure<AuthOptions>(config.GetSection("Auth"));
builder.Services.Configure<RateLimitOptions>(config.GetSection("RateLimiting"));
```

## External System Models

### HMAC Validation Integration

```csharp
// Consumer implements this contract
public interface IHmacValidator
{
    bool Validate(string clientId, string hmac, HttpRequest request, string timestamp);
}

// Canonical request format (consumer-defined, but recommended)
var canonical = $"{method}\n{path}\n{queryString}\n{timestamp}\n{bodyHash}";
// Body hash: SHA256(request body) as Base64, or empty string hash for no body
```

### Client Registry Integration

```csharp
// Consumer implements this contract
public interface IClientRegistry
{
    bool TryGetSecret(string clientId, out string secretBase64);
    bool IsActive(string clientId);
    ClientMetadata GetMetadata(string clientId);
}

public sealed class ClientMetadata
{
    public string ClientId { get; init; }
    public string Name { get; init; }
    public string[] AllowedIpRanges { get; init; }
    public string[] Scopes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
```

### Rate Limiting Integration

```csharp
// Consumer implements this contract
public interface IRateLimiter
{
    Task<RateLimitResult> TryAcquireAsync(string key, int limit, TimeSpan window);
}

public sealed class RateLimitResult
{
    public bool Allowed { get; init; }
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public TimeSpan RetryAfter { get; init; }
    public string ResetHeaderValue => $"{(int)RetryAfter.TotalSeconds}";
}
```

### Audit Logging Integration

```csharp
// Consumer implements this contract
public interface IAuditLogger
{
    Task LogAsync(AuditEvent auditEvent);
}

public sealed class AuditEvent
{
    public string EventType { get; init; } // "AUTH_SUCCESS", "AUTH_FAILURE", "RATE_LIMITED", etc.
    public string ClientId { get; init; }
    public string RequestId { get; init; }
    public string ClientIp { get; init; }
    public string UserAgent { get; init; }
    public string Path { get; init; }
    public string Method { get; init; }
    public int StatusCode { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Details { get; init; } = new();
}
```

### DNS/Hostname Integration

The library uses `NuciAPI.NetworkUtils.GetHostnames(ip)` for reverse DNS.

```csharp
// NuciAPI package provides:
public static class NetworkUtils
{
    public static List<string> GetHostnames(string ipAddress);
    // Returns empty list on failure, never throws
}
```

Consumers can wrap for caching:

```csharp
public interface IDnsCache
{
    bool TryGet(string ip, out List<string> hostnames);
    void Set(string ip, List<string> hostnames, TimeSpan ttl);
}
```

## Health Check Integration

```csharp
// Consumer adds health checks
builder.Services.AddHealthChecks()
    .AddCheck<ClientRegistryHealthCheck>("client-registry")
    .AddCheck<DnsResolutionHealthCheck>("dns-resolution")
    .AddCheck<HmacValidatorHealthCheck>("hmac-validator");

public sealed class ClientRegistryHealthCheck : IHealthCheck
{
    private readonly IClientRegistry _registry;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // Verify registry accessible
            var test = _registry.IsActive("health-check-test");
            return Task.FromResult(HealthCheckResult.Healthy("Client registry accessible"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Client registry unavailable", ex));
        }
    }
}
```

## Metrics Integration

```csharp
// Consumer adds metrics (OpenTelemetry, Prometheus, etc.)
public sealed class MetricsMiddleware(RequestDelegate next, IMeterFactory meterFactory) : NuciApiMiddleware(next)
{
    private readonly Counter<long> _requestCounter = meterFactory.CreateMeter("NuciAPI.Middleware")
        .CreateCounter<long>("middleware_requests_total");
    private readonly Histogram<double> _requestDuration = meterFactory.CreateMeter("NuciAPI.Middleware")
        .CreateHistogram<double>("middleware_request_duration_seconds");

    public override async Task InvokeAsync(HttpContext context)
    {
        var clientId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.ClientId) ?? "unknown";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Next(context);
            _requestCounter.Add(1, new("client_id", clientId), new("status", context.Response.StatusCode.ToString()));
        }
        catch (Exception)
        {
            _requestCounter.Add(1, new("client_id", clientId), new("status", "error"));
            throw;
        }
        finally
        {
            _requestDuration.Record(stopwatch.Elapsed.TotalSeconds, new("client_id", clientId));
        }
    }
}
```

## Testing Integration

### Test Doubles for Integration Points

```csharp
// Test implementations
public sealed class TestHmacValidator : IHmacValidator
{
    public bool ShouldValidate { get; set; } = true;
    public bool Validate(string clientId, string hmac, HttpRequest request, string timestamp) => ShouldValidate;
}

public sealed class TestClientRegistry : IClientRegistry
{
    private readonly Dictionary<string, (string Secret, bool Active)> _clients = new();

    public void AddClient(string clientId, string secret, bool active = true)
        => _clients[clientId] = (secret, active);

    public bool TryGetSecret(string clientId, out string secretBase64)
    {
        if (_clients.TryGetValue(clientId, out var client) && client.Active)
        {
            secretBase64 = client.Secret;
            return true;
        }
        secretBase64 = null;
        return false;
    }

    public bool IsActive(string clientId) => _clients.TryGetValue(clientId, out var c) && c.Active;
    public ClientMetadata GetMetadata(string clientId) => new() { ClientId = clientId };
}

public sealed class TestRateLimiter : IRateLimiter
{
    public bool Allow { get; set; } = true;
    public Task<RateLimitResult> TryAcquireAsync(string key, int limit, TimeSpan window)
        => Task.FromResult(new RateLimitResult { Allowed = Allow, Limit = limit, Remaining = Allow ? limit - 1 : 0 });
}
```

## Related Documents

- [Components: Host and Composition](../components/host-and-composition.md) — Pipeline integration
- [Security](../security.md) — HMAC integration details
- [API Usage Examples](../api-usage-examples.md) — Complete integration examples
- [Testing](../testing.md) — Test strategies