# Concurrency and Scheduling

This document describes concurrency behavior, threading model, and scheduling considerations for NuciAPI.Middleware.

## Threading Model

### ASP.NET Core Request Processing

```
Thread Pool (Kestrel/IIS)
         │
         ▼
Request → HttpContext allocated per request
         │
         ▼
Middleware pipeline executes on SAME thread (async/await)
         │
         ▼
await Next(context) → yields thread, resumes on any thread pool thread
         │
         ▼
Response → HttpContext released
```

### Key Properties

| Property | Behavior |
|----------|----------|
| **Per-request isolation** | Each request gets its own `HttpContext` |
| **No shared state in base** | `NuciApiMiddleware` has no instance fields except `Next` |
| **Thread-safe helpers** | All protected methods are stateless (except `Next` delegate) |
| **Async all the way** | `InvokeAsync` returns `Task`; no blocking calls |

## Base Class Concurrency Safety

### NuciApiMiddleware Fields

```csharp
public abstract class NuciApiMiddleware
{
    // Set once in constructor, never modified
    protected RequestDelegate Next { get; }

    // No other instance fields
    // All helpers are stateless (static or use only parameters)
}
```

- `Next` is `readonly` — set in constructor, never changed
- No mutable shared state → inherently thread-safe
- Multiple requests execute `InvokeAsync` concurrently on different threads safely

### Helper Methods Thread Safety

| Method | Thread Safety | Reason |
|--------|---------------|--------|
| `GetHeaderValue` | ✅ Safe | Reads `HttpRequest.Headers` (per-request) |
| `TryGetHeaderValue` | ✅ Safe | Reads `HttpRequest.Headers` (per-request) |
| `GetClientIpAddress` | ✅ Safe | Reads `HttpContext` (per-request) |
| `GetClientHostname` | ✅ Safe | Calls `NetworkUtils.GetHostnames` (stateless) |
| `UrlDecode` | ✅ Safe | `static`, no shared state |

## Derived Middleware Concurrency

### Safe Patterns

```csharp
// ✅ SAFE: Stateless, uses only per-request context
public sealed class SafeMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        string ip = GetClientIpAddress(context); // Per-request
        await Next(context);
    }
}

// ✅ SAFE: Injected services are typically scoped/singleton with internal sync
public sealed class ServiceMiddleware(
    RequestDelegate next,
    IRateLimiter rateLimiter) : NuciApiMiddleware(next)
{
    private readonly IRateLimiter _rateLimiter = rateLimiter;

    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        await _rateLimiter.AcquireAsync(clientId); // Service handles concurrency
        await Next(context);
    }
}
```

### Unsafe Patterns to Avoid

```csharp
// ❌ UNSAFE: Mutable instance field shared across requests
public sealed class UnsafeMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    private int _requestCount = 0; // SHARED across all requests!

    public override async Task InvokeAsync(HttpContext context)
    {
        _requestCount++; // Race condition!
        await Next(context);
    }
}

// ❌ UNSAFE: Capturing request-scoped data in instance field
public sealed class AlsoUnsafe(RequestDelegate next) : NuciApiMiddleware(next)
{
    private string _lastClientId; // Overwritten by concurrent requests

    public override async Task InvokeAsync(HttpContext context)
    {
        _lastClientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        await Next(context);
        // _lastClientId may be from different request here!
    }
}
```

### Correct State Management

```csharp
// ✅ CORRECT: Use HttpContext.Items for per-request state
public sealed class CorrectMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);

        // Store in request-scoped dictionary
        context.Items["ClientId"] = clientId;

        await Next(context);

        // Retrieve from same request's Items
        var stored = context.Items["ClientId"] as string;
    }
}

// ✅ CORRECT: Use scoped services for request-scoped state
public interface IRequestContext
{
    string ClientId { get; set; }
}

public sealed class RequestContext : IRequestContext
{
    public string ClientId { get; set; }
}

// Register: builder.Services.AddScoped<IRequestContext, RequestContext>();

public sealed class ScopedMiddleware(
    RequestDelegate next,
    IRequestContext requestContext) : NuciApiMiddleware(next)
{
    private readonly IRequestContext _requestContext = requestContext;

    public override async Task InvokeAsync(HttpContext context)
    {
        _requestContext.ClientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        await Next(context);
        // _requestContext is scoped to this request
    }
}
```

## Async/Await Behavior

### ConfigureAwait

```csharp
// Library code: no ConfigureAwait needed (ASP.NET Core handles context)
// Consumer code: typically not needed in middleware

public override async Task InvokeAsync(HttpContext context)
{
    // Runs on request thread, no SynchronizationContext to capture
    await Next(context); // Resumes on thread pool thread

    // After await: no UI context, ConfigureAwait(false) is default behavior
}
```

### Cancellation Tokens

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // HttpContext.RequestAborted fires when client disconnects
    var cancellationToken = context.RequestAborted;

    try
    {
        await Next(context);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // Client disconnected — normal, don't log as error
        return;
    }
}
```

## Parallelism in Derived Middleware

### Parallel Operations (When Needed)

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);

    // Parallel independent operations
    var validationTask = _validator.ValidateAsync(clientId, context.Request);
    var rateLimitTask = _rateLimiter.CheckAsync(clientId);
    var auditTask = _auditLogger.LogRequestAsync(clientId, context.Request);

    await Task.WhenAll(validationTask, rateLimitTask, auditTask);

    if (!validationTask.Result)
    {
        context.Response.StatusCode = 401;
        return;
    }

    await Next(context);
}
```

### SemaphoreSlim for Resource Limiting

```csharp
public sealed class LimitedConcurrencyMiddleware(
    RequestDelegate next,
    IOptions<ConcurrencyOptions> options) : NuciApiMiddleware(next)
{
    private readonly SemaphoreSlim _semaphore = new(options.Value.MaxConcurrent);

    public override async Task InvokeAsync(HttpContext context)
    {
        await _semaphore.WaitAsync(context.RequestAborted);
        try
        {
            await Next(context);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

## Scheduling Considerations

### No Background Work in Middleware

```csharp
// ❌ BAD: Fire-and-forget background work
public override async Task InvokeAsync(HttpContext context)
{
    _ = Task.Run(() => _analytics.TrackAsync(...)); // Untracked!
    await Next(context);
}

// ✅ GOOD: Await background work or use proper background service
public override async Task InvokeAsync(HttpContext context)
{
    await _analytics.TrackAsync(...); // Await it
    await Next(context);
}

// ✅ GOOD: Use IHostedService / BackgroundService for true background work
```

### Long-Running Operations

```csharp
// If middleware must do long work, consider:
// 1. Offload to background service via queue
// 2. Return 202 Accepted with polling endpoint
// 3. Use WebSockets/Server-Sent Events for streaming

public override async Task InvokeAsync(HttpContext context)
{
    if (context.Request.Path == "/api/long-operation")
    {
        var jobId = await _jobQueue.EnqueueAsync(new LongJob { ... });
        context.Response.StatusCode = 202;
        await context.Response.WriteAsJsonAsync(new { jobId, statusUrl = $"/api/jobs/{jobId}" });
        return;
    }

    await Next(context);
}
```

## Performance Characteristics

### Allocation Profile

| Operation | Allocations | Notes |
|-----------|-------------|-------|
| `GetHeaderValue` | ~1 string (decoded) | `UrlDecode` allocates if `%` present |
| `TryGetHeaderValue` | ~1 string or null | Same |
| `GetClientIpAddress` | ~2-5 strings | Header parsing, normalization |
| `GetClientHostname` | List<string> + DNS | Network call, allocations |
| `UrlDecode` | 0 or 1 string | No alloc if no `%` |

### Hot Path Optimization

```csharp
// Minimize allocations in hot path
public override async Task InvokeAsync(HttpContext context)
{
    // ✅ GOOD: Single header read, no LINQ
    var headers = context.Request.Headers;
    string clientId = headers[NuciApiHeaderNames.ClientId].FirstOrDefault();

    // ❌ AVOID: Multiple allocations
    // var clientId = context.Request.Headers
    //     .Where(h => h.Key == NuciApiHeaderNames.ClientId)
    //     .Select(h => h.Value.FirstOrDefault())
    //     .FirstOrDefault();

    await Next(context);
}
```

## Testing Concurrency

```csharp
[Test]
public async Task Given_ConcurrentRequests_When_InvokeAsync_Then_NoSharedStateCorruption()
{
    // Arrange
    var middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);
    var contexts = Enumerable.Range(0, 100)
        .Select(i => CreateContext($"client-{i}"))
        .ToArray();

    // Act: Run all concurrently
    var tasks = contexts.Select(c => middleware.InvokeAsync(c));
    await Task.WhenAll(tasks);

    // Assert: Each context processed independently
    foreach (var context in contexts)
    {
        Assert.That(context.Response.StatusCode, Is.EqualTo(200));
    }
}

private static DefaultHttpContext CreateContext(string clientId)
{
    var context = new DefaultHttpContext();
    context.Request.Headers[NuciApiHeaderNames.ClientId] = clientId;
    return context;
}
```

## Related Documents

- [Middleware Invocation Flow](flows/middleware-invocation.md) — Request lifecycle
- [Application Services](components/application-services.md) — Helper method details
- [Error Handling](./error-handling.md) — Cancellation handling
- [Logging](./logging.md) — Correlation across async boundaries