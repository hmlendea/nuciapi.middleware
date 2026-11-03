# Error Handling

This document describes error handling patterns in NuciAPI.Middleware and how derived middleware should handle errors.

## Base Class Error Behavior

### Exceptions Thrown by Base Helpers

| Method | Exception | Condition | HTTP Status |
|--------|-----------|-----------|-------------|
| `GetHeaderValue` | `BadHttpRequestException` | Required header missing | 400 Bad Request |
| `GetHeaderValue` | `ArgumentNullException` | `request` or `headerName` is null | 500 (programming error) |
| `GetClientIpAddress` | None | Never throws | N/A |
| `TryGetHeaderValue` | None | Never throws | N/A |
| `GetClientHostname` | None | Never throws | N/A |
| `UrlDecode` | None | Never throws | N/A |

### Exception Propagation

```
GetHeaderValue throws BadHttpRequestException
         │
         ▼
ASP.NET Core middleware pipeline catches
         │
         ▼
DeveloperExceptionPageMiddleware (dev) OR
ExceptionHandlerMiddleware (prod)
         │
         ▼
Returns 400 Bad Request with error details
         │
         ▼
Pipeline SHORT-CIRCUITS — Next(context) NOT called
```

## Error Handling Patterns for Consumers

### Pattern 1: Let Base Exceptions Propagate (Recommended for Required Headers)

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // Throws BadHttpRequestException if missing → 400 automatically
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
    
    // Validate HMAC
    if (!_hmacValidator.Validate(clientId, hmac, context.Request))
    {
        context.Response.StatusCode = 401;
        return; // Short-circuit, don't call Next
    }
    
    await Next(context);
}
```

### Pattern 2: Handle Optional Headers Gracefully

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // Returns null if missing — no exception
    string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
    string timestamp = TryGetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
    
    // Use for correlation if present
    if (requestId != null)
    {
        context.Items["CorrelationId"] = requestId;
    }
    
    await Next(context);
}
```

### Pattern 3: Custom Validation with Custom Responses

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    
    // Custom business validation
    if (!_clientRegistry.IsActive(clientId))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Client inactive");
        return;
    }
    
    if (!_rateLimiter.TryAcquire(clientId))
    {
        context.Response.StatusCode = 429;
        context.Response.Headers.RetryAfter = "60";
        return;
    }
    
    await Next(context);
}
```

### Pattern 4: Try/Catch for Downstream Errors

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    try
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        await Next(context);
    }
    catch (BadHttpRequestException)
    {
        // Already handled by ASP.NET Core — rethrow or let propagate
        throw;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Log unexpected errors
        _logger.LogError(ex, "Middleware error for client {ClientId}", 
            context.Request.Headers[NuciApiHeaderNames.ClientId].FirstOrDefault());
        
        // Don't expose internal errors
        context.Response.StatusCode = 500;
    }
}
```

## HTTP Status Code Guidelines

| Scenario | Status Code | Notes |
|----------|-------------|-------|
| Missing required header | 400 | `BadHttpRequestException` from base |
| Invalid HMAC/signature | 401 | Unauthorized |
| Client not found/inactive | 403 | Forbidden |
| Rate limit exceeded | 429 | Too Many Requests, include `Retry-After` |
| Downstream service error | 502/503 | Bad Gateway / Service Unavailable |
| Unexpected exception | 500 | Internal Server Error |

## Error Response Format

For consistent API errors, consumers should standardize:

```csharp
// Helper method in derived middleware
private static async Task WriteErrorResponse(HttpContext context, int statusCode, string code, string message)
{
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";
    
    var problem = new
    {
        type = $"https://httpstatuses.com/{statusCode}",
        title = GetTitle(statusCode),
        status = statusCode,
        code = code,
        detail = message,
        instance = context.Request.Path
    };
    
    await context.Response.WriteAsJsonAsync(problem);
}

private static string GetTitle(int statusCode) => statusCode switch
{
    400 => "Bad Request",
    401 => "Unauthorized",
    403 => "Forbidden",
    429 => "Too Many Requests",
    500 => "Internal Server Error",
    502 => "Bad Gateway",
    503 => "Service Unavailable",
    _ => "Error"
};
```

## Logging Errors

```csharp
public sealed class MyMiddleware(RequestDelegate next, ILogger<MyMiddleware> logger) 
    : NuciApiMiddleware(next)
{
    private readonly ILogger<MyMiddleware> _logger = logger;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        try
        {
            string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
            _logger.LogInformation("Request from client {ClientId}", clientId);
            
            await Next(context);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            throw; // Let ASP.NET Core handle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in middleware");
            throw;
        }
    }
}
```

## Testing Error Scenarios

```csharp
[Test]
public async Task Given_MissingRequiredHeader_When_InvokeAsync_Then_Returns400()
{
    // Arrange
    var middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);
    var context = new DefaultHttpContext();
    // No X-Client-ID header
    
    // Act & Assert
    var ex = Assert.ThrowsAsync<BadHttpRequestException>(() => 
        middleware.InvokeAsync(context));
    
    Assert.That(ex.Message, Does.Contain("X-Client-ID"));
}

[Test]
public async Task Given_InvalidHmac_When_InvokeAsync_Then_Returns401()
{
    // Arrange
    var middleware = new MyMiddleware(_ => Task.CompletedTask, _mockHmacValidator);
    var context = new DefaultHttpContext();
    context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-1";
    context.Request.Headers[NuciApiHeaderNames.HmacToken] = "invalid";
    _mockHmacValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpRequest>()))
        .Returns(false);
    
    // Act
    await middleware.InvokeAsync(context);
    
    // Assert
    Assert.That(context.Response.StatusCode, Is.EqualTo(401));
}
```

## Related Documents

- [Header Access Flow](flows/header-access.md) — Base helper error behavior
- [Middleware Invocation Flow](flows/middleware-invocation.md) — Pipeline short-circuiting
- [Security](./security.md) — Security-related error handling
- [Testing](testing.md) — Test strategies for error paths