# Presentation Layer (Public API Surface)

This document details the public API surface of NuciAPI.Middleware: the two types consumers interact with directly.

## NuciApiMiddleware

**File:** `NuciAPI.Middleware/NuciApiMiddleware.cs`
**Namespace:** `NuciAPI.Middleware`
**Accessibility:** `public abstract`

### Constructor

```csharp
protected NuciApiMiddleware(RequestDelegate next)
```

- **Parameter:** `next` — The next middleware in the pipeline. Throws `ArgumentNullException` if null.
- **Visibility:** `protected` — Only derived classes can instantiate.

### Abstract Method

```csharp
public abstract Task InvokeAsync(HttpContext context)
```

- **Purpose:** Entry point for derived middleware logic. Must be implemented by consumers.
- **Contract:** Call `await Next(context)` to continue the pipeline (or omit for terminal middleware).
- **Exception handling:** Unhandled exceptions propagate to ASP.NET Core error handling.

### Protected Helpers

#### GetHeaderValue

```csharp
protected string GetHeaderValue(HttpRequest request, string headerName)
```

- **Behavior:** Reads `request.Headers[headerName]`, takes first value, URL-decodes via `UrlDecode`.
- **Failure:** Throws `BadHttpRequestException` if header is missing.
- **Use case:** Required headers (e.g., `X-Client-ID`, `X-HMAC`).

#### TryGetHeaderValue

```csharp
protected string TryGetHeaderValue(HttpRequest request, string headerName)
```

- **Behavior:** Reads header if present, URL-decodes, returns `null` if missing.
- **Failure:** Returns `null` (no exception).
- **Use case:** Optional headers (e.g., `X-Request-ID`, `X-Timestamp`).

#### GetClientIpAddress

```csharp
protected string GetClientIpAddress(HttpContext context)
```

- **Behavior:** Iterates `ClientIpForwardingHeaderNames` in order:
  1. `X-Forwarded-For`
  2. `Forwarded`
  3. `X-Real-IP`
  4. `CF-Connecting-IP`
  5. `True-Client-IP`
- For each header: splits candidates, normalizes via `TryNormalizeIpAddress`, validates via `IPAddress.TryParse`.
- Returns first valid IP, or falls back to `context.Connection.RemoteIpAddress?.ToString()`.
- **Override point:** `protected` and non-sealed — consumers can override for custom logic.

#### GetClientHostname

```csharp
protected List<string> GetClientHostname(HttpContext context)
```

- **Behavior:** Calls `NetworkUtils.GetHostnames(GetClientIpAddress(context))` from NuciAPI package.
- **Failure:** Returns empty list on DNS failure (no exception).

#### UrlDecode

```csharp
protected static string UrlDecode(string text)
```

- **Behavior:** If string contains `%`, attempts `Uri.UnescapeDataString`. On `UriFormatException`, returns original text.
- **Use case:** Decoding header values that may be URL-encoded.

### Private Implementation (Not for Consumer Use)

- `ClientIpForwardingHeaderNames` — Hardcoded `string[]` of 5 header names
- `TryGetClientIpFromHeaderValue` — Parses header value into IP candidates
- `GetForwardedHeaderIpCandidates` — Parses `Forwarded` header format (`for=...`)
- `TryNormalizeIpAddress` — Trims, unquotes, removes IPv6 brackets, strips ports, filters "unknown", validates via `IPAddress.TryParse`

## NuciApiHeaderNames

**File:** `NuciAPI.Middleware/NuciApiHeaderNames.cs`
**Namespace:** `NuciAPI.Middleware`
**Accessibility:** `public static`

### Constants

| Constant | Value | Purpose |
|----------|-------|---------|
| `ClientId` | `"X-Client-ID"` | Client identification |
| `HmacToken` | `"X-HMAC"` | HMAC authentication token |
| `RequestId` | `"X-Request-ID"` | Request correlation ID |
| `Timestamp` | `"X-Timestamp"` | Request timestamp |

All are `public static string` properties with expression-bodied getters.

## Usage Pattern

```csharp
public sealed class MyMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        // Required header - throws if missing
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);

        // Optional header - returns null if missing
        string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);

        // Client IP from forwarding headers or connection
        string clientIp = GetClientIpAddress(context);

        // Optional hostname resolution
        List<string> hostnames = GetClientHostname(context);

        // Custom logic here...

        await Next(context); // Continue pipeline
    }
}
```

Registration in `Program.cs`:
```csharp
app.UseMiddleware<MyMiddleware>();
```

## Versioning Contract

- **Breaking changes:** Constructor signature, `InvokeAsync` signature, `NuciApiHeaderNames` constant values, helper method signatures, exception types
- **Non-breaking:** Private implementation changes, new protected helpers (if added), internal logic improvements
- **License:** GPL-3.0-or-later — consumers must comply