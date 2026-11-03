# API Reference Index

This section provides detailed reference documentation for all public APIs in NuciAPI.Middleware.

## Packages

| Package | Version | Target Framework | Description |
|---------|---------|------------------|-------------|
| `NuciAPI.Middleware` | 1.0.0+ | net10.0 | ASP.NET Core middleware base utilities |

## Namespaces

| Namespace | Description |
|-----------|-------------|
| `NuciAPI.Middleware` | Main middleware base class and header constants |

## Types

### NuciApiMiddleware (Abstract Base Class)

**Namespace:** `NuciAPI.Middleware`
**Assembly:** `NuciAPI.Middleware.dll`

Base class for building custom ASP.NET Core middleware with common utilities for header access, client IP extraction, and hostname resolution.

#### Constructor

```csharp
protected NuciApiMiddleware(RequestDelegate next)
```

**Parameters:**
- `next` — The next middleware delegate in the pipeline

**Exceptions:**
- `ArgumentNullException` — If `next` is null

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Next` | `RequestDelegate` | The next middleware delegate (readonly) |

#### Methods

| Method | Description |
|--------|-------------|
| `InvokeAsync(HttpContext)` | **Abstract** — Must be implemented by derived classes |
| `GetHeaderValue(HttpRequest, string)` | Read required header with URL decoding (throws if missing) |
| `TryGetHeaderValue(HttpRequest, string)` | Read optional header with URL decoding (returns null if missing) |
| `GetClientIpAddress(HttpContext)` | Extract client IP from forwarding headers or connection |
| `GetClientHostname(HttpContext)` | Reverse DNS lookup for client IP |
| `UrlDecode(string)` | URL-decode string with fallback on malformed input |

[Full Reference →](./nuci-api-middleware.md)

---

### NuciApiHeaderNames (Static Class)

**Namespace:** `NuciAPI.Middleware`
**Assembly:** `NuciAPI.Middleware.dll`

Constants for standard header names used by the middleware ecosystem.

#### Fields

| Field | Value | Description |
|-------|-------|-------------|
| `ClientId` | `"X-Client-ID"` | Required client identifier |
| `RequestId` | `"X-Request-ID"` | Optional correlation ID |
| `Timestamp` | `"X-Timestamp"` | Optional request timestamp |
| `HmacToken` | `"X-Hmac-Token"` | Required HMAC signature |

[Full Reference →](./nuci-api-header-names.md)

---

## Extension Points

### For Derived Middleware

| Extension Point | Type | Description |
|-----------------|------|-------------|
| `InvokeAsync` | Abstract method | Main pipeline logic — must call `Next(context)` to continue |
| `GetClientIpAddress` | Virtual method | Override for custom IP extraction logic |
| Constructor injection | Pattern | Accept additional services via constructor |

### For Consumers

| Extension Point | Type | Description |
|-----------------|------|-------------|
| `IHmacValidator` | Interface (consumer-defined) | Validate HMAC signatures |
| `IClientRegistry` | Interface (consumer-defined) | Look up client credentials |
| `IRateLimiter` | Interface (consumer-defined) | Rate limiting logic |
| `ForwardedHeadersOptions` | ASP.NET Core options | Configure trusted proxies for IP extraction |

---

## Version Compatibility

| NuciAPI.Middleware | .NET | ASP.NET Core | NuciAPI Dependency |
|--------------------|------|--------------|-------------------|
| 1.x | 10.0 | 10.0 | 3.6.0 |

---

## See Also

- [Components: Presentation](../components/presentation.md) — Public API surface overview
- [Flows: Middleware Invocation](../flows/middleware-invocation.md) — Execution flow
- [Flows: Header Access](../flows/header-access.md) — Header reading patterns
- [Flows: Client IP Extraction](../flows/client-ip-extraction.md) — IP extraction details
- [Security](../security.md) — HMAC and security patterns
- [Testing](../testing.md) — Test strategies