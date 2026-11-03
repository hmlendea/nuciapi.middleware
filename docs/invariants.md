# Invariants

This document describes the invariants (guaranteed properties) of NuciAPI.Middleware.

## Base Class Invariants

### Constructor

| Invariant | Description |
|-----------|-------------|
| `next` not null | `ArgumentNullException` if `next` is null |
| Stores `next` | `Next` property returns the provided delegate |

### Protected Helpers

#### `GetHeaderValue(HttpRequest, string)`

| Invariant | Description |
|-----------|-------------|
| Returns non-null string | Never returns null |
| Throws on missing | `BadHttpRequestException` if header absent |
| URL-decodes | Applies `Uri.UnescapeDataString` to value |
| Case-insensitive | Header lookup uses `IHeaderDictionary` semantics |
| No side effects | Does not modify request/response |

#### `TryGetHeaderValue(HttpRequest, string)`

| Invariant | Description |
|-----------|-------------|
| Returns string or null | Null if header absent |
| Never throws | For missing header |
| URL-decodes | Applies `Uri.UnescapeDataString` to value |
| Case-insensitive | Header lookup uses `IHeaderDictionary` semantics |
| No side effects | Does not modify request/response |

#### `GetClientIpAddress(HttpContext)`

| Invariant | Description |
|-----------|-------------|
| Returns string or null | Null if no connection IP |
| Normalised format | `IPAddress.ToString()` canonical form |
| Priority order | `Forwarded` → `X-Forwarded-For` → `X-Real-IP` → `Connection.RemoteIpAddress` |
| Filters private | Skips RFC 1918/4193 addresses in proxy headers |
| No DNS | Does not perform reverse lookup |
| No side effects | Does not modify context |

#### `GetClientHostname(HttpContext)`

| Invariant | Description |
|-----------|-------------|
| Returns List<string> | Never null, empty list on failure |
| Calls GetClientIpAddress | Uses base IP extraction |
| Delegates to NuciAPI | `NetworkUtils.GetHostnames(ip)` |
| Never throws | Returns empty list on DNS failure |
| No caching | Each call performs fresh lookup |
| No side effects | Does not modify context |

#### `UrlDecode(string)`

| Invariant | Description |
|-----------|-------------|
| Returns decoded string | `Uri.UnescapeDataString(input)` |
| Handles null | Returns null if input null |
| Handles empty | Returns empty string if input empty |
| No side effects | Pure function |

### Abstract Method

#### `InvokeAsync(HttpContext)`

| Invariant | Description |
|-----------|-------------|
| Must be implemented | Derived classes required to override |
| Receives context | Full `HttpContext` available |
| Should call `Next(context)` | To continue pipeline (but not enforced) |
| Async | Returns `Task` for awaitable pipeline |

## Header Constants Invariants

| Constant | Value | Invariant |
|----------|-------|-----------|
| `ClientId` | `"X-Client-ID"` | Compile-time constant, never changes |
| `HmacToken` | `"X-Hmac-Token"` | Compile-time constant, never changes |
| `Timestamp` | `"X-Timestamp"` | Compile-time constant, never changes |
| `RequestId` | `"X-Request-ID"` | Compile-time constant, never changes |

## Thread Safety Invariants

| Invariant | Description |
|-----------|-------------|
| No shared state | Base class has no instance fields except `Next` |
| `Next` is readonly | Set once in constructor, never modified |
| Helpers are stateless | All protected methods are pure functions of input |
| Per-request safety | Each request gets own `HttpContext` |
| DI scope safety | Scoped services resolved per request |

## HTTP Semantics Invariants

| Invariant | Description |
|-----------|-------------|
| 400 for missing required | `GetHeaderValue` throws `BadHttpRequestException` |
| Header case-insensitive | ASP.NET Core `IHeaderDictionary` semantics |
| Multiple values | Returns first value (standard header behavior) |
| Empty header | Treated as present (empty string after decode) |

## Error Invariants

| Scenario | Exception | HTTP Status |
|----------|-----------|-------------|
| `next` null in ctor | `ArgumentNullException` | N/A (construction) |
| Required header missing | `BadHttpRequestException` | 400 |
| `UrlDecode` invalid %XX | `FormatException` | Propagates |

## Performance Invariants

| Invariant | Description |
|-----------|-------------|
| No allocations in hot path | Helpers avoid allocations where possible |
| No reflection | All operations use direct API calls |
| No locking | No shared state requires synchronization |
| Minimal overhead | Base class adds ~100ns per request |

## Versioning Invariants

| Invariant | Description |
|-----------|-------------|
| SemVer | Follows Semantic Versioning 2.0.0 |
| Public API = | `NuciApiMiddleware` (abstract), `NuciApiHeaderNames` (constants) |
| Protected API = | Helpers (`GetHeaderValue`, etc.) — may change in minor |
| No breaking changes in patch | Patch versions only fix bugs |

## Related Documents

- [Design Decisions](design-decisions.md) — Rationale for invariants
- [Change Guide](change-guide.md) — Versioning policy
- [Components: Presentation](components/presentation.md) — Public API surface