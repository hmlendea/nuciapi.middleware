# NuciApiMiddleware API Reference

**Namespace:** `NuciAPI.Middleware`
**Assembly:** `NuciAPI.Middleware.dll`
**Source:** `NuciApiMiddleware.cs`

---

## Class Definition

```csharp
public abstract class NuciApiMiddleware
{
    protected NuciApiMiddleware(RequestDelegate next);

    protected RequestDelegate Next { get; }

    public abstract Task InvokeAsync(HttpContext context);

    protected string GetHeaderValue(HttpRequest request, string headerName);
    protected string TryGetHeaderValue(HttpRequest request, string headerName);
    protected string GetClientIpAddress(HttpContext context);
    protected List<string> GetClientHostname(HttpContext context);
    protected static string UrlDecode(string text);
}
```

---

## Constructor

### `NuciApiMiddleware(RequestDelegate next)`

Initializes a new instance of the middleware base class.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `next` | `RequestDelegate` | The next middleware in the pipeline |

**Exceptions:**
| Exception | Condition |
|-----------|-----------|
| `ArgumentNullException` | `next` is `null` |

**Example:**
```csharp
public sealed class MyMiddleware(RequestDelegate next, IMyService service)
    : NuciApiMiddleware(next)
{
    private readonly IMyService _service = service;

    public override async Task InvokeAsync(HttpContext context)
    {
        // Custom logic
        await Next(context);
    }
}
```

---

## Properties

### `Next`

```csharp
protected RequestDelegate Next { get; }
```

The next middleware delegate in the pipeline. Set in constructor, immutable.

**Remarks:** Call `await Next(context)` in `InvokeAsync` to continue the pipeline.

---

## Abstract Method

### `InvokeAsync(HttpContext context)`

```csharp
public abstract Task InvokeAsync(HttpContext context);
```

**Must be implemented by derived classes.** Contains the middleware's request processing logic.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `context` | `HttpContext` | The current HTTP context |

**Returns:** `Task` representing the asynchronous operation.

**Typical Implementation:**
```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // 1. Pre-processing (read headers, validate, etc.)
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);

    // 2. Business logic
    if (!await _validator.ValidateAsync(clientId, context.Request))
    {
        context.Response.StatusCode = 401;
        return; // Short-circuit
    }

    // 3. Continue pipeline
    await Next(context);

    // 4. Post-processing (optional)
    _logger.LogInformation("Request completed for {ClientId}", clientId);
}
```

---

## Protected Methods

### `GetHeaderValue(HttpRequest request, string headerName)`

```csharp
protected string GetHeaderValue(HttpRequest request, string headerName)
```

Reads a **required** header value with URL decoding. Throws if header is missing.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `request` | `HttpRequest` | The HTTP request (typically `context.Request`) |
| `headerName` | `string` | Header name (use `NuciApiHeaderNames` constants) |

**Returns:** `string` — The URL-decoded header value (never null if no exception).

**Exceptions:**
| Exception | Condition |
|-----------|-----------|
| `BadHttpRequestException` | Header not present in request |
| `ArgumentNullException` | `request` or `headerName` is null |

**Example:**
```csharp
// Throws 400 if missing
string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
```

**Behavior:**
1. Checks `request.Headers.ContainsKey(headerName)`
2. If missing → throws `BadHttpRequestException` with message `"The '{headerName}' header is missing."`
3. If present → calls `TryGetHeaderValue` to get and decode first value

---

### `TryGetHeaderValue(HttpRequest request, string headerName)`

```csharp
protected string TryGetHeaderValue(HttpRequest request, string headerName)
```

Reads an **optional** header value with URL decoding. Returns `null` if missing.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `request` | `HttpRequest` | The HTTP request |
| `headerName` | `string` | Header name |

**Returns:** `string` — The URL-decoded header value, or `null` if header not present.

**Exceptions:** None (swallows `UriFormatException` from decoding).

**Example:**
```csharp
// Returns null if missing - no exception
string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
string timestamp = TryGetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);

if (requestId != null)
{
    context.Items["CorrelationId"] = requestId;
}
```

**Behavior:**
1. Gets `request.Headers[headerName].FirstOrDefault()` → `rawValue`
2. If `rawValue` is null → returns `null`
3. Otherwise → calls `UrlDecode(rawValue)` and returns result

---

### `GetClientIpAddress(HttpContext context)`

```csharp
protected string GetClientIpAddress(HttpContext context)
```

Extracts client IP address from forwarding headers or direct connection.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `context` | `HttpContext` | The HTTP context |

**Returns:** `string` — Normalized IP address (IPv4 or IPv6), or `null` if no connection IP.

**Exceptions:** None.

**Header Priority Order:**
1. `X-Forwarded-For`
2. `Forwarded` (RFC 7239)
3. `X-Real-IP`
4. `CF-Connecting-IP` (Cloudflare)
5. `True-Client-IP` (Akamai)

**Example:**
```csharp
string clientIp = GetClientIpAddress(context);
// clientIp may be null if no connection IP (rare)
if (clientIp != null)
{
    _logger.LogInformation("Client IP: {ClientIp}", clientIp);
}
```

**Override Point:** This method is `protected` and non-sealed. Derived classes can override for custom header sets or trust logic.

**See Also:** [Client IP Extraction Flow](../flows/client-ip-extraction.md)

---

### `GetClientHostname(HttpContext context)`

```csharp
protected List<string> GetClientHostname(HttpContext context)
```

Performs reverse DNS lookup for the client IP address.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `context` | `HttpContext` | The HTTP context |

**Returns:** `List<string>` — Hostnames from reverse DNS (empty list on failure).

**Exceptions:** None (returns empty list on DNS failure).

**Dependencies:** `NuciAPI` package (v3.6.0), `NetworkUtils.GetHostnames`

**Example:**
```csharp
var hostnames = GetClientHostname(context);
if (hostnames.Count > 0)
{
    _logger.LogDebug("Client hostname: {Hostname}", hostnames[0]);
}
```

**Performance Note:** This performs a network DNS lookup. Avoid in hot paths; cache results if needed.

---

### `UrlDecode(string text)`

```csharp
protected static string UrlDecode(string text)
```

URL-decodes a string with fallback on malformed input.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `text` | `string` | The string to decode |

**Returns:** `string` — Decoded string, or original text if:
- Input is null, empty, or whitespace
- Input contains no `%` character
- `Uri.UnescapeDataString` throws `UriFormatException`

**Thread Safety:** Static, stateless, thread-safe.

**Example:**
```csharp
UrlDecode("hello%20world")     // "hello world"
UrlDecode("hello world")       // "hello world" (no %)
UrlDecode("hello%ZZworld")     // "hello%ZZworld" (malformed)
UrlDecode(null)                // null
UrlDecode("")                  // ""
```

---

## Usage Patterns

### Required Headers (Throw on Missing)

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // These throw BadHttpRequestException (400) if missing
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
    string timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);

    // Validate...
    await Next(context);
}
```

### Optional Headers (Null on Missing)

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // These return null if missing - never throw
    string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
    string customHeader = TryGetHeaderValue(context.Request, "X-Custom-Header");

    if (requestId != null)
    {
        context.Response.Headers["X-Request-ID"] = requestId;
    }

    await Next(context);
}
```

### Client IP with Fallback

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    string clientIp = GetClientIpAddress(context);

    // Use for rate limiting, geo-blocking, logging
    if (!_rateLimiter.TryAcquire(clientIp))
    {
        context.Response.StatusCode = 429;
        return;
    }

    await Next(context);
}
```

### Custom IP Extraction (Override)

```csharp
public sealed class CustomIpMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    protected override string GetClientIpAddress(HttpContext context)
    {
        // Check custom header first
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

---

## Thread Safety

| Member | Thread Safe? | Notes |
|--------|--------------|-------|
| Constructor | N/A | Called once per middleware instance |
| `Next` property | ✅ | Immutable after construction |
| `GetHeaderValue` | ✅ | Reads per-request `HttpRequest` |
| `TryGetHeaderValue` | ✅ | Reads per-request `HttpRequest` |
| `GetClientIpAddress` | ✅ | Reads per-request `HttpContext` |
| `GetClientHostname` | ✅ | Calls stateless `NetworkUtils.GetHostnames` |
| `UrlDecode` | ✅ | Static, no shared state |

---

## Source Locations

| Member | File | Lines |
|--------|------|-------|
| Class definition | `NuciApiMiddleware.cs` | 1-22 |
| Constructor | `NuciApiMiddleware.cs` | 23-26 |
| `Next` property | `NuciApiMiddleware.cs` | 12 |
| `InvokeAsync` | `NuciApiMiddleware.cs` | 28 |
| `GetHeaderValue` | `NuciApiMiddleware.cs` | 43-53 |
| `TryGetHeaderValue` | `NuciApiMiddleware.cs` | 55-66 |
| `GetClientIpAddress` | `NuciApiMiddleware.cs` | 78-108 |
| `GetClientHostname` | `NuciApiMiddleware.cs` | 74-76 |
| `UrlDecode` | `NuciApiMiddleware.cs` | 110-122 |
| Private IP parsing helpers | `NuciApiMiddleware.cs` | 124-195 |

---

## See Also

- [NuciApiHeaderNames](./nuci-api-header-names.md) — Header constants
- [Middleware Invocation Flow](../flows/middleware-invocation.md) — Pipeline integration
- [Header Access Flow](../flows/header-access.md) — Header reading details
- [Client IP Extraction Flow](../flows/client-ip-extraction.md) — IP extraction details
- [Application Services](../components/application-services.md) — Helper method contracts
- [Security](../security.md) — HMAC validation patterns