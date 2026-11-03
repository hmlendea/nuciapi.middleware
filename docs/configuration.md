# Configuration

This document describes how NuciAPI.Middleware is configured and what configuration options are available.

## Configuration Model

**NuciAPI.Middleware has no built-in configuration system.** It does not:
- Read from `appsettings.json`
- Bind to `IOptions<T>` or `IConfiguration`
- Require any configuration section
- Have a `Configure` method or options class

All behavior is controlled through:
1. **Code** — Derived middleware implementation
2. **ASP.NET Core pipeline ordering** — Where middleware is registered
3. **ASP.NET Core services** — DI registrations the consumer provides

## What Consumers Configure

### Middleware Registration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Optional: Configure forwarded headers for IP extraction
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));
});

var app = builder.Build();

// Order matters: UseForwardedHeaders BEFORE custom middleware
app.UseForwardedHeaders();

// Register derived middleware
app.UseMiddleware<MyNuciApiMiddleware>();

app.Run();
```

### Dependency Injection

Consumers register services their middleware needs:

```csharp
builder.Services.AddSingleton<IHmacValidator, MyHmacValidator>();
builder.Services.AddScoped<IAuditLogger, MyAuditLogger>();

// Middleware receives via constructor
public sealed class MyNuciApiMiddleware(RequestDelegate next, IHmacValidator hmac)
    : NuciApiMiddleware(next)
{
    private readonly IHmacValidator _hmac = hmac;
    // ...
}
```

### Forwarded Headers (Critical for IP Extraction)

The base class `GetClientIpAddress` reads forwarding headers directly from `HttpRequest.Headers`. It does **not** use `HttpContext.Connection.RemoteIpAddress` after `UseForwardedHeaders` modifies it.

**Required setup for correct IP extraction behind proxies:**

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    // Add your trusted proxy IPs/ranges
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
});

var app = builder.Build();
app.UseForwardedHeaders(); // Must be BEFORE UseMiddleware
app.UseMiddleware<MyNuciApiMiddleware>();
```

Without this, `GetClientIpAddress` may read spoofed headers.

## No Configuration Classes

| Pattern | Supported? | Notes |
|---------|------------|-------|
| `IOptions<T>` | No | No options classes exist |
| `IConfiguration` binding | No | Library doesn't read config |
| `appsettings.json` section | No | No schema defined |
| Environment variables | No | Not read directly |
| Command-line args | No | Not read directly |

## Extending with Configuration

If a derived middleware needs configuration, the consumer defines it:

```csharp
// Consumer's configuration class
public sealed class MyMiddlewareOptions
{
    public string RequiredHeaderName { get; set; } = "X-Client-ID";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public string[] AllowedIpRanges { get; set; } = [];
}

// Consumer registers and configures
builder.Services.Configure<MyMiddlewareOptions>(builder.Configuration.GetSection("MyMiddleware"));

// Middleware consumes via IOptions
public sealed class MyNuciApiMiddleware(
    RequestDelegate next,
    IOptions<MyMiddlewareOptions> options) : NuciApiMiddleware(next)
{
    private readonly MyMiddlewareOptions _options = options.Value;
    // ...
}
```

## Version Compatibility

| .NET Version | Configuration Support |
|--------------|----------------------|
| .NET 8.0+ | Full (as described) |
| .NET 10.0 (target) | Full |

## Related Documents

- [Security](./security.md) — Forwarded headers trust configuration
- [Build and Deployment](./build-and-deployment.md) — CI/CD configuration
- [Middleware Invocation Flow](flows/middleware-invocation.md) — Pipeline ordering