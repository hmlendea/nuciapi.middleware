# Host and Composition

This document describes how NuciAPI.Middleware integrates with the ASP.NET Core host and how consumers compose middleware into the request pipeline.

## ASP.NET Core Integration

NuciAPI.Middleware is a **library**, not a host. It has no `Program.cs`, no `Startup.cs`, no `IHostBuilder`, and no dependency injection registration. It targets `net10.0` with a `FrameworkReference` to `Microsoft.AspNetCore.App`.

### Framework Reference

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

This provides:
- `Microsoft.AspNetCore.Http` — `HttpContext`, `HttpRequest`, `HttpResponse`, `RequestDelegate`, `BadHttpRequestException`
- `Microsoft.AspNetCore.Http.Abstractions` — Header collections, connection info
- No additional ASP.NET Core packages required

## Middleware Registration

Consumers register derived middleware in their application's `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
// ... other services ...

var app = builder.Build();

// Register custom middleware derived from NuciApiMiddleware
app.UseMiddleware<MyCustomMiddleware>();

app.MapControllers();
app.Run();
```

### Registration Mechanics

- `app.UseMiddleware<T>()` — ASP.NET Core instantiates `T` per request (transient) by default
- Constructor receives `RequestDelegate next` — the next middleware in the pipeline
- The middleware becomes part of the request delegate chain

### Ordering

Middleware executes in registration order:
```csharp
app.UseMiddleware<FirstMiddleware>();    // Executes first (outermost)
app.UseMiddleware<SecondMiddleware>();   // Executes second
app.UseMiddleware<ThirdMiddleware>();    // Executes third (innermost, before controllers)
app.MapControllers();
```

Each middleware can:
- Run code before `await Next(context)` (request processing)
- Run code after `await Next(context)` (response processing)
- Short-circuit by not calling `Next(context)` (terminal middleware)

## Composition Patterns

### Pattern 1: Single Custom Middleware

```csharp
public sealed class ApiKeyMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        string apiKey = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        // Validate apiKey...
        await Next(context);
    }
}
```

### Pattern 2: Multiple Middleware Layers

```csharp
// Outer: Logging, correlation
app.UseMiddleware<CorrelationMiddleware>();

// Middle: Authentication, rate limiting
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();

// Inner: Request enrichment
app.UseMiddleware<RequestEnrichmentMiddleware>();

app.MapControllers();
```

### Pattern 3: Terminal Middleware (Short-Circuit)

```csharp
public sealed class HealthCheckMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/health")
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Healthy");
            return; // Short-circuit - does not call Next(context)
        }
        await Next(context);
    }
}
```

## Lifetime and Scoping

- **Per-request instantiation:** ASP.NET Core creates a new instance of the middleware class for each request (when registered via `UseMiddleware<T>()`)
- **Constructor runs per request:** `RequestDelegate next` is provided per request
- **No singleton/shared state:** The base class has no static state; derived classes should not use static mutable state
- **Thread safety:** Each request gets its own instance; no synchronization needed for instance members

## Dependency Injection

NuciAPI.Middleware **does not use or require** dependency injection:
- No `IServiceCollection` extensions
- No `IOptions` configuration
- No scoped/transient/singleton registration
- Consumers can inject services into their derived middleware via constructor injection if needed:

```csharp
public sealed class MyMiddleware(RequestDelegate next, IMyService service)
    : NuciApiMiddleware(next)
{
    private readonly IMyService _service = service;

    public override async Task InvokeAsync(HttpContext context)
    {
        // Use _service...
        await Next(context);
    }
}
```

Registration:
```csharp
builder.Services.AddScoped<IMyService, MyService>();
app.UseMiddleware<MyMiddleware>(); // DI resolves IMyService
```

## Hosting Requirements

- **Target Framework:** `net10.0` (aligned with `Microsoft.AspNetCore.App`)
- **Runtime:** ASP.NET Core 10.0 host
- **No separate process:** Runs in-process within the consumer's application
- **No configuration files:** No `appsettings.json`, no environment variables required by the library

## Testing Composition

The test double `TestNuciApiMiddleware` (in `NuciAPI.Middleware.UnitTests.TestDoubles`) demonstrates composition:

```csharp
internal sealed class TestNuciApiMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override Task InvokeAsync(HttpContext context) => Next(context);

    // Exposes protected helpers for testing
    public string GetHeader(HttpContext context, string headerName)
        => GetHeaderValue(context.Request, headerName);
    // ... other exposed helpers
}
```

Unit tests instantiate with `new TestNuciApiMiddleware(_ => Task.CompletedTask)` and call helpers directly.