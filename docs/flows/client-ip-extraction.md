# Client IP Extraction Flow

This document details the complete flow for extracting and normalizing client IP addresses from HTTP forwarding headers.

## Overview

`GetClientIpAddress(HttpContext context)` is the primary entry point. It checks a hardcoded list of forwarding headers in priority order, parses each, normalizes the IP address, and falls back to the direct connection IP.

## Header Priority Order

```csharp
private static readonly string[] ClientIpForwardingHeaderNames =
[
    "X-Forwarded-For",      // 1st priority - most common
    "Forwarded",            // 2nd priority - RFC 7239 standard
    "X-Real-IP",            // 3rd priority - nginx convention
    "CF-Connecting-IP",     // 4th priority - Cloudflare
    "True-Client-IP"        // 5th priority - Akamai/other CDNs
];
```

## Flow Diagram

```mermaid
flowchart TD
    Start([GetClientIpAddress called]) --> LoopStart{For each header in<br/>ClientIpForwardingHeaderNames}
    LoopStart --> GetHeader[Read header value<br/>context.Request.Headers[name].FirstOrDefault()]
    GetHeader --> CheckNull{Value null or<br/>whitespace?}
    CheckNull -->|Yes| NextHeader[Continue to next header]
    CheckNull -->|No| ParseHeader[TryGetClientIpFromHeaderValue<br/>(headerName, value, out ip)]
    ParseHeader --> CheckResult{Returns true?}
    CheckResult -->|Yes| ReturnIP[Return normalized IP]
    CheckResult -->|No| NextHeader
    NextHeader --> MoreHeaders{More headers<br/>in array?}
    MoreHeaders -->|Yes| LoopStart
    MoreHeaders -->|No| Fallback[Return context.Connection.<br/>RemoteIpAddress?.ToString()]
    Fallback --> End([Return IP or null])
    ReturnIP --> End
```

## Detailed Parsing Logic

### For `X-Forwarded-For`, `X-Real-IP`, `CF-Connecting-IP`, `True-Client-IP`

```
Header value: "unknown, 198.51.100.10, 198.51.100.20"
                    │
                    ▼
Split by ',' → ["unknown", " 198.51.100.10", " 198.51.100.20"]
                    │
                    ▼
For each candidate: Trim() → "unknown", "198.51.100.10", "198.51.100.20"
                    │
                    ▼
TryNormalizeIpAddress(candidate)
                    │
                    ├─ "unknown" → filtered out (returns false)
                    ├─ "198.51.100.10" → IPAddress.TryParse succeeds → returns "198.51.100.10"
                    └─ (stops at first valid)
```

### For `Forwarded` Header (RFC 7239)

```
Header value: "for=unknown;proto=https, for=\"[2001:db8:cafe::17]:4711\";proto=https"
                    │
                    ▼
Split by ',' → ["for=unknown;proto=https", " for=\"[2001:db8:cafe::17]:4711\";proto=https"]
                    │
                    ▼
For each entry, split by ';' → tokens
                    │
                    ▼
Find tokens starting with "for=" (case-insensitive)
                    │
                    ▼
Extract value after "for=" → "unknown", "\"[2001:db8:cafe::17]:4711\""
                    │
                    ▼
TryNormalizeIpAddress on each
                    │
                    ├─ "unknown" → filtered out
                    └─ "\"[2001:db8:cafe::17]:4711\"" → normalize → "2001:db8:cafe::17"
```

## Normalization Logic (`TryNormalizeIpAddress`)

```mermaid
flowchart TD
    Input([rawValue]) --> Trim[Trim whitespace]
    Trim --> Unquote[Trim surrounding quotes]
    Unquote --> CheckUnknown{Equals "unknown"<br/>(case-insensitive)?}
    CheckUnknown -->|Yes| RejectFalse[Return false]
    CheckUnknown -->|No| CheckBracket{Starts with '['?}
    CheckBracket -->|Yes| ExtractBracket[Extract between [ and ]]
    CheckBracket -->|No| CheckPort{Contains ':' and<br/>last ':' looks like port?}
    CheckPort -->|Yes| StripPort[Remove port after last ':']
    CheckPort -->|No| AsIs[Use as-is]
    ExtractBracket --> Validate
    StripPort --> Validate
    AsIs --> Validate
    Validate[IPAddress.TryParse] --> Success{Success?}
    Success -->|Yes| Canonical[normalizedIpAddress = parsed.ToString()]
    Success -->|No| RejectFalse
    Canonical --> ReturnTrue[Return true]
```

### Normalization Examples

| Input | After Trim/Unquote | Bracket/Port Handling | Parsed | Output |
|-------|-------------------|----------------------|--------|--------|
| `" 198.51.100.10 "` | `198.51.100.10` | None | ✓ | `198.51.100.10` |
| `"[2001:db8::1]"` | `[2001:db8::1]` | Extract `2001:db8::1` | ✓ | `2001:db8::1` |
| `"[2001:db8::1]:8080"` | `[2001:db8::1]:8080` | Extract `2001:db8::1` | ✓ | `2001:db8::1` |
| `"192.168.1.1:8080"` | `192.168.1.1:8080` | Strip port → `192.168.1.1` | ✓ | `192.168.1.1` |
| `"unknown"` | `unknown` | Filtered | ✗ | (rejected) |
| `"invalid"` | `invalid` | None | ✗ | (rejected) |
| `""` | `""` | None | ✗ | (rejected) |

## Fallback Behavior

If **no forwarding header yields a valid IP**:

```csharp
return context.Connection.RemoteIpAddress?.ToString();
```

- Returns the direct TCP connection's remote IP
- May be `null` if `RemoteIpAddress` is null (rare, e.g., Unix domain sockets)
- No normalization applied (already an `IPAddress`)

## Security Considerations

### Trust Boundaries

**The library does NOT validate trust.** It processes whatever headers are present. Applications MUST configure ASP.NET Core's `ForwardedHeadersOptions` to trust only known proxies:

```csharp
// In Program.cs - BEFORE UseMiddleware
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    // Add trusted proxy IP ranges
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Example
});
```

### Header Spoofing Risk

Without proper `ForwardedHeadersOptions` configuration:
- Clients can spoof `X-Forwarded-For` with arbitrary IPs
- The library will trust the first valid-looking IP in the header
- This can bypass IP-based rate limiting, geo-blocking, etc.

### Mitigation

1. **Configure `ForwardedHeadersOptions`** in the application (not in this library)
2. **Place middleware AFTER** `app.UseForwardedHeaders()` in pipeline
3. **Override `GetClientIpAddress`** for custom trust logic if needed

## Testing Coverage

Unit tests in `NuciApiMiddlewareTests`:

| Test | Header | Input | Expected Output |
|------|--------|-------|-----------------|
| `Given_XForwardedForHeader_When_GetClientIpAddress_Then_ReturnsFirstValidIp` | `X-Forwarded-For` | `"unknown, 198.51.100.10, 198.51.100.20"` | `"198.51.100.10"` |
| `Given_ForwardedHeaderWithIpv6AndPort_When_GetClientIpAddress_Then_ReturnsNormalizedIp` | `Forwarded` | `"for=unknown;proto=https, for=\"[2001:db8:cafe::17]:4711\";proto=https"` | `"2001:db8:cafe::17"` |
| `Given_NoForwardingHeaders_When_GetClientIpAddress_Then_ReturnsRemoteIpAddress` | (none) | `context.Connection.RemoteIpAddress = 203.0.113.5` | `"203.0.113.5"` |

## Implementation Location

- **Main method:** `NuciApiMiddleware.cs` lines 78-108 (`GetClientIpAddress`)
- **Header array:** Line 14-20 (`ClientIpForwardingHeaderNames`)
- **Parsing:** Lines 124-153 (`TryGetClientIpFromHeaderValue`, `GetForwardedHeaderIpCandidates`)
- **Normalization:** Lines 155-195 (`TryNormalizeIpAddress`)

## Related Flows

- [Middleware Invocation Flow](./middleware-invocation.md) — How `GetClientIpAddress` fits in the pipeline
- [Header Access Flow](./header-access.md) — Similar pattern for header reading