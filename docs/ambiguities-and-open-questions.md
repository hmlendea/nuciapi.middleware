# Ambiguities and Open Questions

This document tracks known ambiguities, design decisions needing clarification, and open questions for NuciAPI.Middleware.

## Current Ambiguities

### 1. IP Extraction Trust Model

**Question:** Should the library provide a way to configure trusted proxy headers instead of hardcoding `ClientIpForwardingHeaderNames`?

**Current State:** Hardcoded array of 5 headers, checked in priority order.

**Options:**
- A: Keep hardcoded (current) — simple, no config needed
- B: Add `IOptions<IpExtractionOptions>` — flexible but adds complexity
- C: Make `ClientIpForwardingHeaderNames` protected virtual property — allows override without DI

**Decision Needed:** Before v2.0 if consumers request customization.

---

### 2. Hostname Resolution Blocking

**Question:** `GetClientHostname` performs synchronous DNS lookup which can block.

**Current State:** Calls `NetworkUtils.GetHostnames(ip)` synchronously.

**Options:**
- A: Keep synchronous — simple, caller can wrap in `Task.Run`
- B: Add `GetClientHostnameAsync` — proper async, breaking change
- C: Add timeout parameter — mitigates but doesn't solve blocking

**Decision Needed:** If performance issues reported.

---

### 3. URL Decoding Error Handling

**Question:** `UrlDecode` silently returns original text on `UriFormatException`. Is this correct?

**Current State:** Catches `UriFormatException`, returns input unchanged.

**Options:**
- A: Keep silent fallback — robust, never throws
- B: Throw on malformed — fail fast, surface client errors
- C: Add `TryUrlDecode` pattern — explicit success/failure

**Decision Needed:** Based on consumer feedback.

---

### 4. Header Name Extensibility

**Question:** Should consumers be able to register custom header constants?

**Current State:** `NuciApiHeaderNames` is `static` with 4 constants.

**Options:**
- A: Keep static — simple, known set
- B: Make extensible via `IHeaderNameProvider` — flexible
- C: Document convention for custom headers — no code change

**Decision Needed:** If ecosystem grows.

---

### 5. Middleware Ordering Requirements

**Question:** How strictly should we document/enforce middleware ordering?

**Current State:** Documentation says `UseForwardedHeaders` before `UseMiddleware`.

**Options:**
- A: Document only — current approach
- B: Add runtime check — detect missing `UseForwardedHeaders`
- C: Provide `IStartupFilter` to auto-insert — magic, may surprise

**Decision Needed:** If misconfiguration common.

---

## Design Questions for Future Versions

### 6. Async Helper Methods

**Question:** Should `GetClientIpAddress` and `GetClientHostname` become async?

**Impact:** Breaking change (MAJOR version).

**Considerations:**
- DNS lookup is inherently async
- IP parsing is fast but header reading could be async in future ASP.NET Core
- Would require `ValueTask` for performance

**Timeline:** v2.0 if needed.

---

### 7. Structured Header Parsing

**Question:** Should we parse structured headers (e.g., `Forwarded` RFC 7239) into objects?

**Current State:** Returns raw string, consumer parses.

**Options:**
- A: Keep raw — flexible, no dependency on parsing library
- B: Add `ParsedForwardedHeader` class — convenient but opinionated
- C: Extension method package — separate, optional

**Decision Needed:** If multiple consumers need same parsing.

---

### 8. Rate Limiting Integration

**Question:** Should the library provide rate limiting primitives?

**Current State:** No — consumers implement `IRateLimiter`.

**Options:**
- A: Keep external — separation of concerns
- B: Add `NuciAPI.Middleware.RateLimiting` package — optional, opinionated
- C: Add abstract `RateLimit` method to base — too prescriptive

**Decision Needed:** Based on demand.

---

### 9. OpenTelemetry Integration

**Question:** Should the library emit OpenTelemetry metrics/traces?

**Current State:** No instrumentation.

**Options:**
- A: Add `NuciAPI.Middleware.OpenTelemetry` package — optional
- B: Built-in with `ActivitySource` — always available, zero-cost if not subscribed
- C: Document manual instrumentation — no library change

**Decision Needed:** When observability requirements clarify.

---

### 10. Header Validation Pipeline

**Question:** Should there be a built-in header validation pipeline?

**Current State:** Consumers validate in `InvokeAsync`.

**Options:**
- A: Keep manual — full control
- B: Add `IHeaderValidator` chain — reusable, composable
- C: Fluent validation API — expressive but complex

**Decision Needed:** If validation logic duplicates across consumers.

---

## Resolved Decisions

| Question | Decision | Version | Rationale |
|----------|----------|---------|-----------|
| Target framework | net10.0 | 1.0 | Align with .NET 10 release |
| Dependency on NuciAPI | Yes, v3.6.0 | 1.0 | Needed for `NetworkUtils.GetHostnames` |
| License | GPL-3.0-or-later | 1.0 | Project requirement |
| Base class abstract | Yes | 1.0 | Forces `InvokeAsync` implementation |
| `GetHeaderValue` throws | Yes | 1.0 | Fail fast for required headers |
| `TryGetHeaderValue` returns null | Yes | 1.0 | Optional header pattern |
| IP header priority | Hardcoded | 1.0 | Common proxy headers, no config needed |
| URL decode fallback | Silent | 1.0 | Robustness over strictness |

---

## Tracking

| ID | Title | Status | Priority | Target Version |
|----|-------|--------|----------|----------------|
| AMB-001 | Configurable IP headers | Open | Low | 2.0 |
| AMB-002 | Async hostname resolution | Open | Medium | 2.0 |
| AMB-003 | URL decode error policy | Open | Low | 1.x |
| AMB-004 | Custom header constants | Open | Low | 2.0 |
| AMB-005 | Middleware ordering enforcement | Open | Low | 1.x |
| AMB-006 | Async IP extraction | Open | Medium | 2.0 |
| AMB-007 | Structured header parsing | Open | Low | 2.0 |
| AMB-008 | Rate limiting package | Open | Low | 2.0 |
| AMB-009 | OpenTelemetry integration | Open | Medium | 2.0 |
| AMB-010 | Header validation pipeline | Open | Low | 2.0 |

---

## Contributing Clarifications

If you encounter an ambiguity not listed here:

1. **Search issues** — https://github.com/hmlendea/nuciapi.middleware/issues
2. **Open discussion** — Use "Question" label
3. **Propose solution** — Include use case and trade-offs
4. **Document decision** — Once resolved, move to "Resolved Decisions"

## Related Documents

- [Change Guide](change-guide.md) — How ambiguities become breaking changes
- [Architecture](architecture.md) — Design context
- [Security](security.md) — Security-related ambiguities
- [Troubleshooting](troubleshooting.md) — Issues from ambiguities