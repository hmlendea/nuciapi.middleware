# Browser State and Localisation

This document describes how NuciAPI.Middleware interacts with browser state and localisation concerns.

## Browser State

### No Direct Browser Interaction

NuciAPI.Middleware is a **server-side ASP.NET Core library**. It does not:
- Set or read cookies directly
- Manage browser storage (localStorage, sessionStorage, IndexedDB)
- Handle browser-specific APIs (Service Workers, Cache API, etc.)
- Perform client-side redirects or navigation

### Indirect Browser State via Headers

Consumers may use the library's header helpers to implement browser state patterns:

```csharp
// Correlation ID from browser (via header)
var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
if (requestId == null)
{
    requestId = Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Request-ID"] = requestId;
}

// Client-generated fingerprint (custom header)
var fingerprint = TryGetHeaderValue(context.Request, "X-Client-Fingerprint");
```

### Session Affinity

The library does not manage session affinity. If consumers need sticky sessions:
- Use `GetClientIpAddress()` for IP-based affinity
- Use custom header (e.g., `X-Session-ID`) with `TryGetHeaderValue()`
- Configure load balancer affinity separately

## Localisation

### No Built-in Localisation

NuciAPI.Middleware does not provide:
- Resource files (.resx)
- Culture detection
- Localised error messages
- Date/number formatting

### Consumer Responsibility

Consumers handle localisation at their layer:

```csharp
public sealed class LocalisedMiddleware(RequestDelegate next, IStringLocalizer<LocalisedMiddleware> localizer)
    : NuciApiMiddleware(next)
{
    private readonly IStringLocalizer _localizer = localizer;

    public override async Task InvokeAsync(HttpContext context)
    {
        var culture = context.Request.Headers["Accept-Language"].FirstOrDefault() ?? "en";

        try
        {
            await Next(context);
        }
        catch (BadHttpRequestException ex) when (ex.Message.Contains("Header"))
        {
            // Return localised error
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(_localizer["MissingRequiredHeader", ex.Message]);
        }
    }
}
```

### Header Values and Culture

Header values are **culture-invariant**:
- Timestamps: ISO 8601 (UTC) — `2024-01-15T10:30:00Z`
- IP addresses: Standard notation — `198.51.100.10`, `2001:db8::1`
- Client IDs: Opaque strings — no culture dependency
- HMAC tokens: Base64 — culture-invariant

### Error Messages

Base class throws `BadHttpRequestException` with English messages:
- `"Required header 'X-Client-ID' is missing."`
- `"Required header 'X-Hmac-Token' is missing."`
- `"Required header 'X-Timestamp' is missing."`

Consumers should catch and localise:

```csharp
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BadHttpRequestException ex)
    {
        var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<Program>>();
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync(localizer["InvalidRequest", ex.Message]);
    }
});
```

## Time Zones

### Timestamp Handling

The `X-Timestamp` header **must be UTC** (ISO 8601 with `Z` or `+00:00`):

```csharp
// Correct
"2024-01-15T10:30:00Z"
"2024-01-15T10:30:00+00:00"

// Incorrect (will parse but may cause validation issues)
"2024-01-15T10:30:00-05:00"
"2024-01-15T10:30:00"
```

### Validation

```csharp
var timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
if (!DateTimeOffset.TryParse(timestamp, out var requestTime))
{
    // Invalid format
    return;
}

// Ensure UTC
if (requestTime.Offset != TimeSpan.Zero)
{
    requestTime = requestTime.ToUniversalTime();
}

// Check freshness
if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
{
    // Replay attack or clock skew
    return;
}
```

## Related Documents

- [Header Access Flow](../flows/header-access.md) — Reading headers
- [Error Handling](../error-handling.md) — Exception handling
- [Security](../security.md) — Timestamp validation
- [API Usage Examples](../api-usage-examples.md) — Complete examples