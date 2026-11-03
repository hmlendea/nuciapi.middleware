# Design Decisions

This document records significant design decisions for NuciAPI.Middleware.

## Decision Log

| ID | Date | Decision | Rationale | Alternatives Considered |
|----|------|----------|-----------|------------------------|
| DD-001 | 2024 | Abstract base class over interface | Enables protected helpers with implementation; avoids boilerplate in derived classes | Interface + extension methods; source generators |
| DD-002 | 2024 | Protected helpers (not public) | Encapsulates middleware-specific logic; prevents misuse outside pipeline | Public static helpers; separate service |
| DD-003 | 2024 | Throw on missing required header | Fail-fast for security-critical headers; clear contract | Return null/option; try-pattern for all |
| DD-004 | 2024 | URL decode header values automatically | Headers often URL-encoded by clients; reduces consumer boilerplate | Raw values only; opt-in decoding |
| DD-005 | 2024 | Multi-header IP extraction priority | Handles diverse proxy/CDN configurations; defense in depth | Single header only; configuration-based |
| DD-006 | 2024 | NuciAPI for hostname resolution | Reuses existing battle-tested DNS logic; avoids duplication | Direct `Dns.GetHostEntry`; System.Net.Http |
| DD-007 | 2024 | No built-in configuration system | Library stays minimal; consumers control config via DI | Options pattern; IConfiguration binding |
| DD-008 | 2024 | No logging in base class | Avoids forcing logging abstraction; consumers choose | ILogger injection; built-in logging |
| DD-009 | 2024 | GPL-3.0-or-later license | Ensures derivative works remain open; aligns with NuciAPI | MIT; Apache-2.0 |
| DD-010 | 2024 | net10.0 target only | Leverages latest .NET features; reduces maintenance burden | Multi-target net8.0/net9.0/net10.0 |
| DD-011 | 2024 | Test double pattern for unit tests | Tests protected helpers without TestHost overhead | Integration tests only; reflection |
| DD-012 | 2024 | Header constants in separate class | Single source of truth; easy to discover and update | Magic strings; enum; resource file |
| DD-013 | 2024 | Constant-time HMAC comparison | Prevents timing attacks on signature validation | Simple string equality |
| DD-014 | 2024 | ForwardedHeaders middleware required | Correct IP extraction depends on it; explicit is better than implicit | Auto-configure; detect automatically |
| DD-015 | 2024 | No async helpers in base class | Keeps base class simple; async not needed for header/IP access | Async GetHeaderValueAsync etc. |

## Detailed Rationale

### DD-001: Abstract Base Class

**Context:** How to provide reusable middleware functionality.

**Decision:** `abstract class NuciApiMiddleware(RequestDelegate next)`

**Reasoning:**
- Protected helpers need access to `HttpContext`/`HttpRequest`
- Derived classes must implement `InvokeAsync` — abstract enforces this
- Single inheritance is acceptable for middleware (rarely need multiple base)
- Avoids `IHttpContextAccessor` anti-pattern

### DD-003: Throw on Missing Required Header

**Context:** `GetHeaderValue` vs `TryGetHeaderValue`

**Decision:** `GetHeaderValue` throws `BadHttpRequestException` (400) if header missing.

**Reasoning:**
- Security headers (ClientId, HMAC, Timestamp) are mandatory for auth
- Fail-fast prevents downstream null reference exceptions
- 400 is correct HTTP semantics for missing required header
- Consumers use `TryGetHeaderValue` for optional headers

### DD-005: IP Extraction Priority Order

**Context:** Multiple proxy headers exist.

**Decision:** Priority: `Forwarded` (RFC 7239) → `X-Forwarded-For` → `X-Real-IP` → `HttpContext.Connection.RemoteIpAddress`

**Reasoning:**
- `Forwarded` is standard, structured, supports multiple proxies
- `X-Forwarded-For` is de facto standard, widely used
- `X-Real-IP` common in nginx single-proxy setups
- Direct connection IP is last resort (no proxy)

### DD-007: No Configuration System

**Context:** Whether to provide `IOptions<MiddlewareOptions>`.

**Decision:** No built-in configuration.

**Reasoning:**
- Middleware registration order is configuration
- DI provides all needed flexibility
- Avoids `Microsoft.Extensions.Options` dependency
- Consumers bind their own config to their services

### DD-010: Single Target Framework

**Context:** Support multiple .NET versions.

**Decision:** Target `net10.0` only.

**Reasoning:**
- .NET 10 is current STS; 9 is LTS
- Multi-targeting increases CI complexity
- Library is small; consumers can multi-target their apps
- Can add net9.0 if demand exists

## Rejected Alternatives

| Alternative | Why Rejected |
|-------------|--------------|
| Source generators for header access | Overkill for 4 headers; adds build complexity |
| `IHeaderDictionary` extension methods | Loses `HttpContext` for IP/hostname; less discoverable |
| Built-in rate limiting | Out of scope; consumers have diverse needs |
| Built-in HMAC validation | Requires secret storage; couples to crypto impl |
| Automatic `UseForwardedHeaders` | Hidden behavior; breaks if proxy config differs |
| Structured header parsing (RFC 8941) | Headers are simple strings; adds dependency |

## Future Considerations

| ID | Topic | Status |
|----|-------|--------|
| DD-F01 | Async helpers for body reading | Deferred |
| DD-F02 | Structured header parsing | Deferred |
| DD-F03 | OpenTelemetry integration | Deferred |
| DD-F04 | Rate limiting abstraction | Deferred |
| DD-F05 | Header validation pipeline | Deferred |

## Related Documents

- [Architecture](architecture.md) — Architectural style
- [Ambiguities and Open Questions](ambiguities-and-open-questions.md) — Open questions
- [Security](security.md) — Security decisions