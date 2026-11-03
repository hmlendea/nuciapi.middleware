# NuciApiHeaderNames API Reference

**Namespace:** `NuciAPI.Middleware`  
**Assembly:** `NuciAPI.Middleware.dll`  
**Source:** `NuciApiHeaderNames.cs`

---

## Class Definition

```csharp
public static class NuciApiHeaderNames
{
    public const string ClientId = "X-Client-ID";
    public const string RequestId = "X-Request-ID";
    public const string Timestamp = "X-Timestamp";
    public const string HmacToken = "X-Hmac-Token";
}
```

---

## Fields

### `ClientId`

```csharp
public const string ClientId = "X-Client-ID";
```

**Value:** `"X-Client-ID"`

**Purpose:** Required header identifying the client making the request.

**Usage:**
```csharp
// Required - throws 400 if missing
string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
```

**Typical Values:** `"client-123"`, `"api-user-456"`, `"service-a"`

**Validation:** Consumers should validate against a client registry.

---

### `RequestId`

```csharp
public const string RequestId = "X-Request-ID";
```

**Value:** `"X-Request-ID"`

**Purpose:** Optional correlation ID for request tracing across services.

**Usage:**
```csharp
// Optional - returns null if missing
string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);

if (requestId != null)
{
    context.Items["CorrelationId"] = requestId;
    context.Response.Headers["X-Request-ID"] = requestId; // Echo back
}
```

**Typical Values:** UUID (`"550e8400-e29b-41d4-a716-446655440000"`), ULID (`"01ARZ3NDEKTSV4RRFFQ69G5FAV"`), custom (`"req-abc-123"`)

**Generation:** If not provided by client, middleware can generate:
```csharp
var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId) 
    ?? Guid.NewGuid().ToString("N");
```

---

### `Timestamp`

```csharp
public const string Timestamp = "X-Timestamp";
```

**Value:** `"X-Timestamp"`

**Purpose:** Optional request timestamp for replay protection (typically used with HMAC).

**Usage:**
```csharp
// Optional - returns null if missing
string timestamp = TryGetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);

if (timestamp != null && DateTimeOffset.TryParse(timestamp, out var requestTime))
{
    var age = DateTimeOffset.UtcNow - requestTime;
    if (age > TimeSpan.FromMinutes(5))
    {
        context.Response.StatusCode = 401; // Replay attack protection
        return;
    }
}
```

**Format:** ISO 8601 / RFC 3339 (e.g., `"2025-01-15T10:30:45.123Z"`, `"2025-01-15T10:30:45+00:00"`)

**Validation:** Consumers should:
- Parse as `DateTimeOffset`
- Reject if older than threshold (e.g., 5 minutes)
- Reject if in future (clock skew tolerance)

---

### `HmacToken`

```csharp
public const string HmacToken = "X-Hmac-Token";
```

**Value:** `"X-Hmac-Token"`

**Purpose:** Required HMAC signature for request authentication and integrity.

**Usage:**
```csharp
// Required - throws 400 if missing
string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);

if (!_hmacValidator.Validate(clientId, hmac, context.Request, timestamp))
{
    context.Response.StatusCode = 401;
    return;
}
```

**Format:** Base64-encoded HMAC-SHA256 (typically 44 chars for SHA256)

**Algorithm:** HMAC-SHA256 (recommended)

**Validation:** Consumers implement `IHmacValidator`:
```csharp
public interface IHmacValidator
{
    bool Validate(string clientId, string hmac, HttpRequest request, string timestamp);
}
```

---

## Usage with Base Class Helpers

### Required Headers Pattern

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // All throw BadHttpRequestException (400) if missing
    string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
    string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
    string timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
    
    // Validate...
    await Next(context);
}
```

### Optional Headers Pattern

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    // All return null if missing - never throw
    string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
    
    // Use for correlation
    if (requestId != null)
    {
        context.Items["CorrelationId"] = requestId;
    }
    
    await Next(context);
}
```

---

## Header Name Conventions

| Aspect | Convention |
|--------|------------|
| **Prefix** | `X-` (de facto standard for custom headers) |
| **Casing** | Kebab-case (`X-Client-ID`, not `X-Client-Id`) |
| **Case Sensitivity** | HTTP headers are case-insensitive |
| **Constants** | Use `NuciApiHeaderNames` constants — avoid string literals |

---

## Client Implementation Guide

### Sending Required Headers

```http
POST /api/resource HTTP/1.1
Host: api.example.com
X-Client-ID: client-123
X-Hmac-Token: base64-encoded-hmac
X-Timestamp: 2025-01-15T10:30:45.123Z
Content-Type: application/json

{"data": "value"}
```

### Sending Optional Headers

```http
GET /api/resource HTTP/1.1
Host: api.example.com
X-Client-ID: client-123
X-Hmac-Token: base64-encoded-hmac
X-Timestamp: 2025-01-15T10:30:45.123Z
X-Request-ID: 550e8400-e29b-41d4-a716-446655440000
```

---

## Testing

```csharp
[TestFixture]
public class NuciApiHeaderNamesTests
{
    [Test]
    public void ClientId_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.ClientId, Is.EqualTo("X-Client-ID"));
    }
    
    [Test]
    public void RequestId_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.RequestId, Is.EqualTo("X-Request-ID"));
    }
    
    [Test]
    public void Timestamp_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.Timestamp, Is.EqualTo("X-Timestamp"));
    }
    
    [Test]
    public void HmacToken_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.HmacToken, Is.EqualTo("X-Hmac-Token"));
    }
}
```

---

## Source Location

| Field | File | Line |
|-------|------|------|
| `ClientId` | `NuciApiHeaderNames.cs` | 10 |
| `RequestId` | `NuciApiHeaderNames.cs` | 11 |
| `Timestamp` | `NuciApiHeaderNames.cs` | 12 |
| `HmacToken` | `NuciApiHeaderNames.cs` | 13 |

---

## See Also

- [NuciApiMiddleware](./nuci-api-middleware.md) — Base class using these constants
- [Header Access Flow](../flows/header-access.md) — Reading headers
- [Security](../security.md) — HMAC validation implementation
- [Middleware Invocation Flow](../flows/middleware-invocation.md) — Pipeline integration