# State and Persistence

This document describes state management and persistence patterns for NuciAPI.Middleware and derived implementations.

## Base Class State

**NuciAPI.Middleware maintains no persistent state.** It is a stateless middleware base class:

```csharp
public abstract class NuciApiMiddleware
{
    // Only field - set once, immutable
    protected RequestDelegate Next { get; }
    
    // No other fields
    // No persistence, no caching, no background state
}
```

### Implications

| Aspect | Behavior |
|--------|----------|
| **Horizontal scaling** | Trivial — any instance handles any request |
| **Restart safety** | No state loss on restart |
| **Memory profile** | Constant — no accumulation |
| **Serialization** | Not applicable |

## Per-Request State (HttpContext.Items)

The standard ASP.NET Core pattern for request-scoped state:

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    
    // Store for downstream middleware/controllers
    context.Items["ClientId"] = clientId;
    context.Items["RequestStartTime"] = DateTimeOffset.UtcNow;
    
    await Next(context);
    
    // Access in later middleware or controllers
    var startTime = (DateTimeOffset)context.Items["RequestStartTime"];
    var duration = DateTimeOffset.UtcNow - startTime;
}
```

### Best Practices

```csharp
// ✅ GOOD: Use constant keys to avoid typos
public static class MiddlewareContextKeys
{
    public const string ClientId = "NuciApi.ClientId";
    public const string RequestId = "NuciApi.RequestId";
    public const string ClientIp = "NuciApi.ClientIp";
}

// ✅ GOOD: Extension methods for typed access
public static class HttpContextExtensions
{
    public static string GetClientId(this HttpContext context)
        => context.Items.TryGetValue(MiddlewareContextKeys.ClientId, out var value) 
            ? value as string : null;
    
    public static void SetClientId(this HttpContext context, string clientId)
        => context.Items[MiddlewareContextKeys.ClientId] = clientId;
}
```

## Scoped Services for Request State

For richer request-scoped state, use DI scoped services:

```csharp
// Service definition
public interface IRequestContext
{
    string ClientId { get; set; }
    string RequestId { get; set; }
    string ClientIp { get; set; }
    DateTimeOffset StartTime { get; }
    IDictionary<string, object> Properties { get; }
}

public sealed class RequestContext : IRequestContext
{
    public string ClientId { get; set; }
    public string RequestId { get; set; }
    public string ClientIp { get; set; }
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}

// Registration
builder.Services.AddScoped<IRequestContext, RequestContext>();

// Usage in middleware
public sealed class MyMiddleware(
    RequestDelegate next, 
    IRequestContext requestContext) : NuciApiMiddleware(next)
{
    private readonly IRequestContext _requestContext = requestContext;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        _requestContext.ClientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        _requestContext.RequestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId) 
            ?? Guid.NewGuid().ToString("N");
        _requestContext.ClientIp = GetClientIpAddress(context);
        
        await Next(context);
    }
}

// Usage in controllers
[ApiController]
[Route("api/[controller]")]
public sealed class MyController(IRequestContext requestContext) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { requestContext.ClientId, requestContext.RequestId });
    }
}
```

## Persistent State (Consumer Responsibility)

NuciAPI.Middleware does not provide persistence. Consumers implement their own:

### Common Patterns

| Pattern | Use Case | Implementation |
|---------|----------|----------------|
| **Database** | Client registry, audit logs | EF Core, Dapper, raw SQL |
| **Distributed Cache** | Rate limiting, session data | Redis (StackExchange.Redis), SQL Server |
| **In-Memory** | Single-instance rate limiting | `MemoryCache`, `ConcurrentDictionary` |
| **External API** | Third-party auth, validation | `HttpClient` with Polly |

### Example: Rate Limiting with Redis

```csharp
public interface IRateLimiter
{
    Task<bool> TryAcquireAsync(string clientId, int limit, TimeSpan window);
}

public sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _db;
    
    public RedisRateLimiter(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }
    
    public async Task<bool> TryAcquireAsync(string clientId, int limit, TimeSpan window)
    {
        var key = $"ratelimit:{clientId}";
        var count = await _db.StringIncrementAsync(key);
        
        if (count == 1)
        {
            await _db.KeyExpireAsync(key, window);
        }
        
        return count <= limit;
    }
}

// Registration
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]));
builder.Services.AddScoped<IRateLimiter, RedisRateLimiter>();
```

### Example: Client Registry with EF Core

```csharp
public sealed class Client
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string HmacSecret { get; set; } // Base64 encoded
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public interface IClientRegistry
{
    Task<Client?> GetAsync(string clientId);
    Task<bool> IsActiveAsync(string clientId);
}

public sealed class EfClientRegistry : IClientRegistry
{
    private readonly AppDbContext _db;
    
    public EfClientRegistry(AppDbContext db) => _db = db;
    
    public async Task<Client?> GetAsync(string clientId)
        => await _db.Clients.FindAsync(clientId);
    
    public async Task<bool> IsActiveAsync(string clientId)
        => await _db.Clients.AnyAsync(c => c.Id == clientId && c.IsActive);
}
```

## No Built-in Caching

The base class does not cache:
- Header values
- IP addresses
- Hostname lookups
- Decoded values

Each call re-reads and re-computes. If consumers need caching, they implement it:

```csharp
public sealed class CachingMiddleware(
    RequestDelegate next, 
    IMemoryCache cache) : NuciApiMiddleware(next)
{
    private readonly IMemoryCache _cache = cache;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        
        // Cache client validation result for 5 minutes
        var isValid = await _cache.GetOrCreateAsync($"client:{clientId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _clientRegistry.IsActiveAsync(clientId);
        });
        
        if (!isValid)
        {
            context.Response.StatusCode = 403;
            return;
        }
        
        await Next(context);
    }
}
```

## Hostname Resolution Caching

`GetClientHostname` calls `NetworkUtils.GetHostnames` which performs DNS reverse lookup. This is **not cached** by the library.

### Consumer Caching Pattern

```csharp
public sealed class HostnameCachingMiddleware(
    RequestDelegate next, 
    IMemoryCache cache) : NuciApiMiddleware(next)
{
    private readonly IMemoryCache _cache = cache;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        string ip = GetClientIpAddress(context);
        
        if (!string.IsNullOrEmpty(ip))
        {
            var hostnames = await _cache.GetOrCreateAsync($"rdns:{ip}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return GetClientHostname(context); // Base method does DNS lookup
            });
            
            context.Items["ClientHostnames"] = hostnames;
        }
        
        await Next(context);
    }
}
```

## State in Testing

### Unit Testing with HttpContext.Items

```csharp
[Test]
public async Task Given_Request_When_InvokeAsync_Then_StoresClientIdInItems()
{
    var middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);
    var context = new DefaultHttpContext();
    context.Request.Headers[NuciApiHeaderNames.ClientId] = "test-client";
    
    await middleware.InvokeAsync(context);
    
    Assert.That(context.Items["NuciApi.ClientId"], Is.EqualTo("test-client"));
}
```

### Testing with Scoped Services

```csharp
[Test]
public async Task Given_ScopedContext_When_InvokeAsync_Then_PopulatesContext()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<IRequestContext, RequestContext>();
    services.AddScoped<TestNuciApiMiddleware>();
    var provider = services.BuildServiceProvider();
    
    using var scope = provider.CreateScope();
    var middleware = scope.ServiceProvider.GetRequiredService<TestNuciApiMiddleware>();
    var context = new DefaultHttpContext();
    context.RequestServices = scope.ServiceProvider;
    context.Request.Headers[NuciApiHeaderNames.ClientId] = "test-client";
    
    // Act
    await middleware.InvokeAsync(context);
    
    // Assert
    var requestContext = scope.ServiceProvider.GetRequiredService<IRequestContext>();
    Assert.That(requestContext.ClientId, Is.EqualTo("test-client"));
}
```

## Persistence Configuration

### Connection Strings

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User=...;Password=...",
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddRedis(builder.Configuration["ConnectionStrings:Redis"]);
```

## Related Documents

- [Concurrency and Scheduling](./concurrency-and-scheduling.md) — Request isolation
- [Configuration](./configuration.md) — DI registration patterns
- [Security](./security.md) — Secure storage of secrets
- [Middleware Invocation Flow](flows/middleware-invocation.md) — Where state fits in pipeline
- [Application Services](components/application-services.md) — Base helpers that don't persist