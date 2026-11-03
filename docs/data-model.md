# Data Model

This document describes the data structures and models used in NuciAPI.Middleware.

## Overview

NuciAPI.Middleware is a **library/SDK**, not an application with a persistent data model. It does not define:
- Database entities
- DTOs for API responses
- Domain models
- Event schemas

Instead, it provides **primitives** that consumers use to build their own data models.

## Core Primitives

### Header Values

All header values are `string` after URL decoding.

| Header | Constant | Type | Required |
|--------|----------|------|----------|
| Client ID | `NuciApiHeaderNames.ClientId` | `string` | Yes (via `GetHeaderValue`) |
| HMAC Token | `NuciApiHeaderNames.HmacToken` | `string` | Yes (via `GetHeaderValue`) |
| Timestamp | `NuciApiHeaderNames.Timestamp` | `string` (ISO 8601) | Yes (via `GetHeaderValue`) |
| Request ID | `NuciApiHeaderNames.RequestId` | `string` | No (via `TryGetHeaderValue`) |

### IP Address

```csharp
// Returned by GetClientIpAddress
string clientIp; // e.g., "198.51.100.10" or "2001:db8::1"
// null if no connection IP available
```

**Format:** Normalized IPv4 or IPv6 (canonical form from `IPAddress.ToString()`)

### Hostnames

```csharp
// Returned by GetClientHostname
List<string> hostnames; // e.g., ["host.example.com", "host2.example.com"]
// Empty list if DNS lookup fails or no IP
```

## Consumer-Defined Models

Consumers typically define these models when using the library:

### Client Identity

```csharp
public sealed class ClientIdentity
{
    public string ClientId { get; init; }
    public string HmacSecretBase64 { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string[] AllowedIpRanges { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = new();
}
```

### Request Context

```csharp
public sealed class RequestContext
{
    public string ClientId { get; set; }
    public string RequestId { get; set; }
    public string ClientIp { get; set; }
    public List<string> ClientHostnames { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset RequestStartTime { get; } = DateTimeOffset.UtcNow;
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
```

### HMAC Validation Result

```csharp
public sealed class HmacValidationResult
{
    public bool IsValid { get; init; }
    public string ClientId { get; init; }
    public string ErrorCode { get; init; } // "INVALID_SIGNATURE", "REPLAY_DETECTED", "CLIENT_NOT_FOUND", etc.
    public string ErrorMessage { get; init; }
}
```

### Rate Limit Info

```csharp
public sealed class RateLimitInfo
{
    public string Key { get; init; }
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public TimeSpan ResetAfter { get; init; }
    public bool IsLimited { get; init; }
}
```

## No Serialization

The library does not serialize/deserialize any data. Consumers handle:
- Request body parsing (JSON, XML, etc.)
- Response formatting
- Configuration binding
- Audit logging

## Related Documents

- [Application Services](components/application-services.md) — Helper return types
- [Security](security.md) — HMAC canonical request format
- [API Usage Examples](api-usage-examples.md) — Model usage in context