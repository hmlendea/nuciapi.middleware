# Logging

This document describes logging in NuciAPI.Middleware and recommended practices for derived middleware.

## Base Class Logging

**NuciAPI.Middleware does not perform any logging itself.** The base class:
- Has no `ILogger` dependency
- Does not log header access, IP extraction, or internal operations
- Leaves all logging to derived middleware implementations

This design keeps the library lightweight and avoids imposing logging frameworks or levels on consumers.

## Recommended Logging for Derived Middleware

### Constructor Injection

```csharp
public sealed class MyNuciApiMiddleware(
    RequestDelegate next, 
    ILogger<MyNuciApiMiddleware> logger) : NuciApiMiddleware(next)
{
    private readonly ILogger<MyNuciApiMiddleware> _logger = logger;
}
```

### Structured Logging with Context

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    var clientId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
    
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["ClientId"] = clientId ?? "unknown",
        ["RequestId"] = requestId ?? "none",
        ["Path"] = context.Request.Path.Value ?? "/",
        ["Method"] = context.Request.Method
    });
    
    _logger.LogInformation("Middleware invoked");
    
    try
    {
        await Next(context);
        _logger.LogInformation("Request completed with status {StatusCode}", context.Response.StatusCode);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Request failed");
        throw;
    }
}
```

### Log Levels Guidance

| Level | When to Use |
|-------|-------------|
| `Trace` | Verbose per-request details (header values, IP, timing) |
| `Debug` | Flow control (entering/exiting methods, branch decisions) |
| `Information` | Key business events (request start/end, auth success/fail) |
| `Warning` | Recoverable issues (rate limit near threshold, deprecated header) |
| `Error` | Unhandled exceptions, validation failures with client impact |
| `Critical` | System-level failures (DI resolution, config missing) |

### Sensitive Data Handling

**Never log sensitive values directly:**

```csharp
// ❌ BAD - logs HMAC token
_logger.LogDebug("HMAC: {Hmac}", hmacToken);

// ✅ GOOD - log presence only
_logger.LogDebug("HMAC header present: {HasHmac}", !string.IsNullOrEmpty(hmacToken));

// ✅ GOOD - log truncated/hash for correlation
_logger.LogDebug("HMAC prefix: {HmacPrefix}", hmacToken?[..Math.Min(8, hmacToken.Length)]);

// ❌ BAD - logs client secret
_logger.LogInformation("Client secret: {Secret}", clientSecret);

// ✅ GOOD - log client ID only
_logger.LogInformation("Authenticating client {ClientId}", clientId);
```

### Correlation IDs

Use `X-Request-ID` header or generate one:

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId) 
        ?? Guid.NewGuid().ToString("N");
    
    context.Response.Headers["X-Request-ID"] = requestId;
    
    using var scope = _logger.BeginScope(new { RequestId = requestId });
    
    _logger.LogInformation("Processing request");
    await Next(context);
}
```

## ASP.NET Core Logging Configuration

Consumers configure logging in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Clear default providers
builder.Logging.ClearProviders();

// Console with structured format
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "json";
});
builder.Logging.AddConsoleFormatter<JsonConsoleFormatter, ConsoleFormatterOptions>();

// Filter levels
builder.Logging.AddFilter("MyApp.Middleware", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// Add other providers (Serilog, Seq, Datadog, etc.)
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:5341"));
```

## Log Output Examples

### Information Level (Request Start/End)

```json
{
  "Timestamp": "2025-01-15T10:30:45.123Z",
  "Level": "Information",
  "Message": "Middleware invoked",
  "ClientId": "client-123",
  "RequestId": "abc123def456",
  "Path": "/api/v1/resource",
  "Method": "POST",
  "SourceContext": "MyApp.Middleware.MyNuciApiMiddleware"
}
```

```json
{
  "Timestamp": "2025-01-15T10:30:45.456Z",
  "Level": "Information",
  "Message": "Request completed with status 200",
  "ClientId": "client-123",
  "RequestId": "abc123def456",
  "StatusCode": 200,
  "DurationMs": 333,
  "SourceContext": "MyApp.Middleware.MyNuciApiMiddleware"
}
```

### Warning Level (Rate Limit)

```json
{
  "Timestamp": "2025-01-15T10:30:45.789Z",
  "Level": "Warning",
  "Message": "Rate limit 90% utilized for client {ClientId}",
  "ClientId": "client-123",
  "CurrentUsage": 90,
  "Limit": 100,
  "SourceContext": "MyApp.Middleware.MyNuciApiMiddleware"
}
```

### Error Level (Validation Failure)

```json
{
  "Timestamp": "2025-01-15T10:30:46.012Z",
  "Level": "Error",
  "Message": "HMAC validation failed for client {ClientId}",
  "ClientId": "client-123",
  "RequestId": "abc123def456",
  "Exception": "System.Security.Cryptography.CryptographicException: Invalid signature...",
  "SourceContext": "MyApp.Middleware.MyNuciApiMiddleware"
}
```

## Performance Considerations

### Avoid Allocation in Hot Paths

```csharp
// ❌ BAD - allocates dictionary every request
_logger.LogInformation("Request {Path} {Method}", context.Request.Path, context.Request.Method);

// ✅ GOOD - use interpolated string (compiled to template)
_logger.LogInformation("Request {Path} {Method}", context.Request.Path.Value, context.Request.Method);

// ✅ GOOD - use LoggerMessage delegate (zero allocation)
private static readonly Action<ILogger, string, string, Exception?> _requestLog = 
    LoggerMessage.Define<string, string>(
        LogLevel.Information,
        new EventId(1, "RequestReceived"),
        "Request {Path} {Method}");

_requestLog(_logger, context.Request.Path.Value!, context.Request.Method, null);
```

### Conditional Logging

```csharp
// ✅ GOOD - check level before expensive computation
if (_logger.IsEnabled(LogLevel.Debug))
{
    var headers = context.Request.Headers
        .Where(h => h.Key.StartsWith("X-"))
        .ToDictionary(h => h.Key, h => h.Value.ToString());
    _logger.LogDebug("Incoming headers: {Headers}", headers);
}
```

## Testing Logging

```csharp
[Test]
public async Task Given_ValidRequest_When_InvokeAsync_Then_LogsInformation()
{
    // Arrange
    var loggerFactory = LoggerFactory.Create(b => b.AddProvider(new TestLoggerProvider()));
    var logger = loggerFactory.CreateLogger<MyNuciApiMiddleware>();
    var middleware = new MyNuciApiMiddleware(_ => Task.CompletedTask, logger);
    var context = new DefaultHttpContext();
    context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-1";
    
    // Act
    await middleware.InvokeAsync(context);
    
    // Assert
    var logs = TestLoggerProvider.GetLogs();
    Assert.That(logs, Has.Some.Matches<LogEntry>(e => 
        e.Level == LogLevel.Information && e.Message.Contains("Middleware invoked")));
}
```

## Related Documents

- [Error Handling](./error-handling.md) — Logging exceptions
- [Security](./security.md) — Sensitive data in logs
- [Middleware Invocation Flow](flows/middleware-invocation.md) — Where to log in pipeline
- [Configuration](./configuration.md) — Logging configuration in DI