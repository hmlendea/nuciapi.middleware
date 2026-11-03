# Application Services (Base Class Helpers)

This document details the protected helper methods provided by `NuciApiMiddleware` that derived middleware use to implement their logic.

## Helper Method Contracts

### GetHeaderValue

```csharp
protected string GetHeaderValue(HttpRequest request, string headerName)
```

**Purpose:** Read a required header value with URL decoding.

**Preconditions:**
- `request` is not null (guaranteed by ASP.NET Core)
- `headerName` is not null/empty (caller responsibility)

**Behavior:**
1. Checks `request.Headers.ContainsKey(headerName)`
2. If missing: throws `BadHttpRequestException` with message `"The '{headerName}' header is missing."`
3. If present: calls `TryGetHeaderValue` to get and decode the first value

**Returns:** Decoded header value (never null if no exception)

**Exceptions:** `BadHttpRequestException` — header missing

**Source:** `NuciApiMiddleware.cs` lines 43-53

---

### TryGetHeaderValue

```csharp
protected string TryGetHeaderValue(HttpRequest request, string headerName)
```

**Purpose:** Read an optional header value with URL decoding.

**Preconditions:** Same as `GetHeaderValue`

**Behavior:**
1. Gets `request.Headers[headerName].FirstOrDefault()` → `rawValue`
2. If `rawValue` is null: returns `null`
3. Otherwise: calls `UrlDecode(rawValue)` and returns result

**Returns:** Decoded header value, or `null` if header missing

**Exceptions:** None (swallows `UriFormatException` in `UrlDecode`)

**Source:** `NuciApiMiddleware.cs` lines 55-66

---

### GetClientIpAddress

```csharp
protected string GetClientIpAddress(HttpContext context)
```

**Purpose:** Extract client IP from forwarding headers or connection.

**Preconditions:**
- `context` is not null
- `context.Request` and `context.Connection` are valid

**Behavior:**
1. Iterates `ClientIpForwardingHeaderNames` in order:
   - `X-Forwarded-For`
   - `Forwarded`
   - `X-Real-IP`
   - `CF-Connecting-IP`
   - `True-Client-IP`
2. For each header name:
   - Gets first header value: `context.Request.Headers[headerName].FirstOrDefault()`
   - If null/whitespace: continues to next header
   - Calls `TryGetClientIpFromHeaderValue(headerName, headerValue, out clientIpAddress)`
   - If returns `true`: returns `clientIpAddress`
3. If no header yields valid IP: returns `context.Connection.RemoteIpAddress?.ToString()` (may be null)

**Returns:** Normalized IP address string (IPv4 or IPv6), or `null` if no connection IP

**Override Point:** `protected` and non-sealed — consumers can override for custom header sets or trust logic

**Source:** `NuciApiMiddleware.cs` lines 78-108

---

### GetClientHostname

```csharp
protected List<string> GetClientHostname(HttpContext context)
```

**Purpose:** Reverse DNS lookup for client IP.

**Behavior:**
1. Calls `GetClientIpAddress(context)` to get IP
2. Calls `NetworkUtils.GetHostnames(ip)` from NuciAPI package
3. Returns result (empty list on failure)

**Returns:** `List<string>` of hostnames (may be empty)

**Dependencies:** `NuciAPI` package (v3.6.0), `NetworkUtils.GetHostnames`

**Source:** `NuciApiMiddleware.cs` lines 74-76

---

### UrlDecode

```csharp
protected static string UrlDecode(string text)
```

**Purpose:** URL-decode a string, with fallback on malformed input.

**Behavior:**
1. If `text` is null/whitespace or does not contain `%`: returns `text` unchanged
2. Otherwise: tries `Uri.UnescapeDataString(text)`
3. On `UriFormatException`: returns `text` unchanged

**Returns:** Decoded string, or original on failure

**Thread Safety:** Static, stateless, thread-safe

**Source:** `NuciAPI.Middleware.cs` lines 110-122

---

## Private Implementation Details (For Context)

These are `private` and not accessible to consumers, but understanding them helps when overriding `GetClientIpAddress`.

### ClientIpForwardingHeaderNames

```csharp
private static readonly string[] ClientIpForwardingHeaderNames =
[
    "X-Forwarded-For",
    "Forwarded",
    "X-Real-IP",
    "CF-Connecting-IP",
    "True-Client-IP"
];
```

**Order matters:** First valid IP wins. Hardcoded — cannot be configured.

### TryGetClientIpFromHeaderValue

```csharp
private static bool TryGetClientIpFromHeaderValue(
    string headerName, 
    string headerValue, 
    out string clientIpAddress)
```

**Behavior:**
- For `Forwarded` header: calls `GetForwardedHeaderIpCandidates` (parses `for=` tokens)
- For others: splits by `,` and trims
- For each candidate: calls `TryNormalizeIpAddress`
- Returns first valid normalized IP

### GetForwardedHeaderIpCandidates

```csharp
private static IEnumerable<string> GetForwardedHeaderIpCandidates(string forwardedHeaderValue)
```

**Parses:** `Forwarded` header format per RFC 7239
- Splits by `,` (multiple forwarded entries)
- Splits each entry by `;` (tokens)
- Finds tokens starting with `for=` (case-insensitive)
- Yields value after `for=` (trimmed)

**Example:** `"for=unknown;proto=https, for=\"[2001:db8:cafe::17]:4711\";proto=https"`
→ Yields: `"unknown"`, `"[2001:db8:cafe::17]:4711"`

### TryNormalizeIpAddress

```csharp
private static bool TryNormalizeIpAddress(string rawValue, out string normalizedIpAddress)
```

**Normalization steps:**
1. Trim whitespace
2. Trim surrounding quotes (`"`)
3. If equals `"unknown"` (case-insensitive): return `false`
4. If starts with `[` (IPv6 with brackets): extract content between `[` and `]`
5. Else if contains `:` and last `:` looks like port (IPv4 with port): strip port
6. Validate via `IPAddress.TryParse`
7. On success: set `normalizedIpAddress = parsedIpAddress.ToString()` (canonical form)

**Returns:** `true` if valid IP parsed, `false` otherwise

**Source:** `NuciApiMiddleware.cs` lines 155-195

---

## Usage Guidelines for Consumers

### Required Headers Pattern

```csharp
// Throws BadHttpRequestException (400) if missing
string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
```

### Optional Headers Pattern

```csharp
// Returns null if missing, never throws
string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
string timestamp = TryGetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);

if (requestId != null) { /* use it */ }
```

### Client IP Pattern

```csharp
// Uses hardcoded header priority, falls back to connection IP
string clientIp = GetClientIpAddress(context);
// clientIp may be null if no connection IP (rare)
```

### Custom IP Extraction (Override)

```csharp
public sealed class MyMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    protected override string GetClientIpAddress(HttpContext context)
    {
        // Custom logic: check custom header first
        if (context.Request.Headers.TryGetValue("X-Custom-Client-IP", out var customIp))
        {
            if (IPAddress.TryParse(customIp, out var parsed))
                return parsed.ToString();
        }
        // Fall back to base implementation
        return base.GetClientIpAddress(context);
    }
    
    public override async Task InvokeAsync(HttpContext context)
    {
        string ip = GetClientIpAddress(context); // Uses overridden version
        await Next(context);
    }
}
```

### Hostname Resolution Pattern

```csharp
List<string> hostnames = GetClientHostname(context);
// hostnames may be empty - handle gracefully
if (hostnames.Count > 0)
{
    string primaryHost = hostnames[0];
    // Use for logging, analytics, etc.
}
```

---

## Error Handling Summary

| Helper | Missing Header | Malformed Value | External Failure |
|--------|----------------|-----------------|------------------|
| `GetHeaderValue` | `BadHttpRequestException` | `UriFormatException` → caught, returns original | N/A |
| `TryGetHeaderValue` | Returns `null` | `UriFormatException` → caught, returns original | N/A |
| `GetClientIpAddress` | Continues to next header | `TryNormalizeIpAddress` returns `false`, continues | Falls back to connection IP |
| `GetClientHostname` | N/A | N/A | Returns empty list |
| `UrlDecode` | N/A | Returns original text | N/A |

---

## Testing Helpers

The test double `TestNuciApiMiddleware` exposes all protected helpers as public methods:

```csharp
// In NuciAPI.Middleware.UnitTests.TestDoubles.TestNuciApiMiddleware
public string GetHeader(HttpContext context, string headerName) 
    => GetHeaderValue(context.Request, headerName);

public string TryGetHeader(HttpContext context, string headerName) 
    => TryGetHeaderValue(context.Request, headerName);

public string GetClientIp(HttpContext context) 
    => GetClientIpAddress(context);

public string Decode(string text) 
    => UrlDecode(text);
```

Unit tests use this to verify helper behavior without needing a full ASP.NET Core host.