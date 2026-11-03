# Frequently Asked Questions

This document answers common questions about NuciAPI.Middleware.

## General

### What is NuciAPI.Middleware?

A minimal ASP.NET Core middleware base library providing:
- Typed header access with URL decoding
- Client IP extraction from proxy headers
- Reverse DNS hostname resolution
- Abstract base class for custom middleware

### Who should use this library?

Developers building custom ASP.NET Core middleware who need:
- Standardised header reading (ClientId, HMAC, Timestamp, RequestId)
- Reliable client IP extraction behind proxies/load balancers
- Hostname resolution for audit/logging
- A clean base class reducing boilerplate

### What does it NOT do?

- No authentication/authorisation logic
- No rate limiting
- No logging
- No configuration system
- No HTTP client functionality
- No request/response transformation

## Installation

### How do I install it?

```bash
dotnet add package NuciAPI.Middleware
```

Requires .NET 10.0+.

### Which .NET versions are supported?

Only **net10.0** (current STS release). The library targets a single framework to minimise maintenance.

### Does it work with .NET 8/9?

Not officially. You can try multi-targeting your application, but the library itself only builds for net10.0.

## Usage

### How do I create custom middleware?

```csharp
public sealed class MyMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        await Next(context);
    }
}
```

Register in `Program.cs`:
```csharp
app.UseForwardedHeaders(); // Required!
app.UseMiddleware<MyMiddleware>();
```

### Why do I need `UseForwardedHeaders()`?

`GetClientIpAddress()` reads `X-Forwarded-For` and `Forwarded` headers. These are only populated by ASP.NET Core's `ForwardedHeadersMiddleware`, which must run **before** your middleware.

```csharp
app.UseForwardedHeaders(); // Must be FIRST
app.UseMiddleware<MyMiddleware>();
```

Configure in `Program.cs`:
```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Your load balancer IP
});
```

### How do I read optional headers?

Use `TryGetHeaderValue` — returns `null` if missing:

```csharp
string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
if (requestId != null)
{
    context.Items["CorrelationId"] = requestId;
}
```

### How do I read required headers?

Use `GetHeaderValue` — throws `BadHttpRequestException` (400) if missing:

```csharp
string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
// If header missing: 400 "Required header 'X-Client-ID' is missing."
```

### What headers are defined?

| Constant | Header Name | Purpose |
|----------|-------------|---------|
| `NuciApiHeaderNames.ClientId` | `X-Client-ID` | Client identifier |
| `NuciApiHeaderNames.HmacToken` | `X-Hmac-Token` | HMAC signature |
| `NuciApiHeaderNames.Timestamp` | `X-Timestamp` | Request timestamp (ISO 8601 UTC) |
| `NuciApiHeaderNames.RequestId` | `X-Request-ID` | Correlation ID (optional) |

### Are header values URL-decoded?

Yes, automatically. Both `GetHeaderValue` and `TryGetHeaderValue` apply `Uri.UnescapeDataString` to the raw header value.

## IP Extraction

### Why does my IP show the load balancer?

You're missing `UseForwardedHeaders()` or it's misconfigured. Ensure:
1. `app.UseForwardedHeaders()` runs before your middleware
2. `KnownProxies` includes your load balancer IP(s)
3. `ForwardedHeaders` includes `XForwardedFor`

### What IP headers are checked?

Priority order:
1. `Forwarded` (RFC 7239) — `for="198.51.100.10"`
2. `X-Forwarded-For` — `198.51.100.10, 203.0.113.50`
3. `X-Real-IP` — `198.51.100.10`
4. `HttpContext.Connection.RemoteIpAddress` — Direct connection

First valid non-private IP wins.

### Can I customise IP extraction?

Yes, override `GetClientIpAddress` in your derived class:

```csharp
protected override string GetClientIpAddress(HttpContext context)
{
    if (context.Request.Headers.TryGetValue("X-Custom-IP", out var custom))
    {
        if (IPAddress.TryParse(custom, out var ip)) return ip.ToString();
    }
    return base.GetClientIpAddress(context);
}
```

## Hostname Resolution

### How does hostname resolution work?

`GetClientHostname(context)` calls `NuciAPI.NetworkUtils.GetHostnames(ip)` which performs reverse DNS lookup.

### Is it cached?

No. The library does no caching. Consumers should implement caching if needed:

```csharp
protected override List<string> GetClientHostname(HttpContext context)
{
    var ip = GetClientIpAddress(context);
    if (ip == null) return [];

    if (_cache.TryGet(ip, out var cached)) return cached;

    var hostnames = base.GetClientHostname(context);
    _cache.Set(ip, hostnames, TimeSpan.FromMinutes(5));
    return hostnames;
}
```

### What if DNS fails?

Returns empty list `[]`. Never throws.

## HMAC / Security

### Does the library validate HMAC?

No. It only provides header access. You implement validation in your derived middleware.

### What's the recommended HMAC format?

See [Security](security.md#hmac-validation-pattern) for canonical request format and constant-time comparison.

### How do I prevent replay attacks?

Validate the `X-Timestamp` header:
```csharp
var timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
var requestTime = DateTimeOffset.Parse(timestamp);
if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
{
    // Reject: too old or future timestamp
}
```

## Testing

### How do I unit test my middleware?

Use the test double pattern:

```csharp
// TestNuciApiMiddleware exposes protected methods as public
var middleware = new TestMyMiddleware(_ => Task.CompletedTask);
var context = new DefaultHttpContext();
context.Request.Headers["X-Client-ID"] = "test-client";

var clientId = middleware.GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
Assert.That(clientId, Is.EqualTo("test-client"));
```

### Can I use TestServer for integration tests?

Yes:
```csharp
var server = new TestServer(new WebHostBuilder()
    .Configure(app => app.UseMiddleware<MyMiddleware>()));
var client = server.CreateClient();
client.DefaultRequestHeaders.Add("X-Client-ID", "test");
var response = await client.GetAsync("/");
```

## Troubleshooting

### "Required header 'X-Client-ID' is missing"

The client didn't send the header. Check:
- Client code includes `X-Client-ID`
- No proxy stripping headers
- Header name matches exactly (case-insensitive in ASP.NET Core)

### IP is always `::1` (localhost)

You're testing locally without `UseForwardedHeaders()`. Either:
- Configure `KnownProxies` with `IPAddress.Loopback` for local testing
- Use a real proxy/load balancer in test environment

### DNS lookup is slow

`GetClientHostname` blocks on DNS. Solutions:
- Cache results (see above)
- Make it async in your override (but base method is sync)
- Skip if not needed

### Build fails with "SDK not found"

Ensure .NET 10.0 SDK is installed:
```bash
dotnet --list-sdks
# Should show 10.0.xxx
```

## Licensing

### What license?

GPL-3.0-or-later. See [LICENSE](../LICENSE).

### Can I use it in commercial projects?

Yes, but derivative works must also be GPL-3.0-or-later. Consult legal counsel.

### Why not MIT/Apache?

Aligns with NuciAPI dependency (also GPL-3.0-or-later).

## Contributing

### How do I contribute?

See [CONTRIBUTING.md](../CONTRIBUTING.md).

### Where do I report bugs?

https://github.com/hmlendea/nuciapi.middleware/issues

### Where do I report security issues?

See [SECURITY.md](../SECURITY.md) — email security@hmlendea.com

## Related Documents

- [Quick Start](quick-start.md) — 5-minute setup
- [API Usage Examples](api-usage-examples.md) — Complete examples
- [Troubleshooting](troubleshooting.md) — Common issues
- [Security](security.md) — Security patterns