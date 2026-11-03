# Quick Start

Get started with NuciAPI.Middleware in 5 minutes.

## Prerequisites

- .NET 10.0 SDK
- ASP.NET Core project

## Installation

```bash
dotnet add package NuciAPI.Middleware
```

## Create Custom Middleware

### 1. Create Your Middleware Class

```csharp
// Middleware/AuthMiddleware.cs
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware;

public sealed class AuthMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        // Read required headers (throws 400 if missing)
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
        string timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
        
        // TODO: Validate HMAC, timestamp, client existence
        
        // Optional: Read correlation ID
        string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
        if (requestId != null)
        {
            context.Response.Headers["X-Request-ID"] = requestId;
        }
        
        // Continue pipeline
        await Next(context);
    }
}
```

### 2. Register in Program.cs

```csharp
// Program.cs
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// REQUIRED: Configure forwarded headers for IP extraction
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Your load balancer IP
});

var app = builder.Build();

// ORDER MATTERS!
app.UseForwardedHeaders();        // 1st: Process proxy headers
app.UseMiddleware<AuthMiddleware>(); // 2nd: Your middleware
app.MapControllers();

app.Run();
```

## Test It

### Using curl

```bash
# Valid request
curl -H "X-Client-ID: my-client" \
     -H "X-Hmac-Token: abc123" \
     -H "X-Timestamp: 2024-01-15T10:30:00Z" \
     http://localhost:5000/api/data

# Missing header → 400 Bad Request
curl http://localhost:5000/api/data
```

### Expected Response

```
# Success (200)
{"message":"OK"}

# Missing X-Client-ID (400)
Required header 'X-Client-ID' is missing.
```

## Next Steps

| Task | Document |
|------|----------|
| Implement HMAC validation | [Security](security.md#hmac-validation-pattern) |
| Add rate limiting | [API Usage Examples](api-usage-examples.md#rate-limiting-middleware) |
| Add logging | [API Usage Examples](api-usage-examples.md#logging-middleware) |
| Customise IP extraction | [Inspect and Modify Behavior](behaviour/inspect-edit-delete.md#override-ip-extraction) |
| Unit test your middleware | [Testing](testing.md#unit-testing) |
| Configure for your cloud | [Integrations](integrations.md#cloud-provider-integration) |

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| IP shows load balancer | Add `app.UseForwardedHeaders()` BEFORE your middleware |
| 400 on valid requests | Check `KnownProxies` includes your proxy IPs |
| DNS timeout | Don't call `GetClientHostname` on every request; cache it |
| HMAC fails | Ensure canonical request format matches client exactly |

## Minimal Working Example

```csharp
// Program.cs - Complete minimal example
using Microsoft.AspNetCore.HttpOverrides;
using NuciAPI.Middleware;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<AuthMiddleware>();

app.MapGet("/api/hello", () => "Hello from NuciAPI.Middleware!");

app.Run();
```

```csharp
// AuthMiddleware.cs
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware;

public sealed class AuthMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        // Just demonstrate header reading
        var clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
        
        context.Response.Headers["X-Client-ID-Echo"] = clientId;
        if (requestId != null) context.Response.Headers["X-Request-ID"] = requestId;
        
        await Next(context);
    }
}
```

Run:
```bash
dotnet run
curl -H "X-Client-ID: test-client" -H "X-Request-ID: req-123" http://localhost:5000/api/hello
# Response: Hello from NuciAPI.Middleware!
# Headers: X-Client-ID-Echo: test-client, X-Request-ID: req-123
```

## Related Documents

- [API Usage Examples](api-usage-examples.md) — Complete examples
- [Components: Presentation](components/presentation.md) — API surface
- [Troubleshooting](troubleshooting.md) — Common issues