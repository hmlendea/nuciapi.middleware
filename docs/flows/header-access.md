# Header Access Flow

This document traces the flow of reading HTTP headers through the base class helpers, including URL decoding and error handling.

## Overview

Two public helpers provide header access:
- `GetHeaderValue` — required headers (throws on missing)
- `TryGetHeaderValue` — optional headers (returns null on missing)

Both apply URL decoding to the raw header value.

## Flow Diagrams

### GetHeaderValue (Required Header)

```mermaid
flowchart TD
    Start([GetHeaderValue called]) --> CheckContains{request.Headers<br/>.ContainsKey(headerName)?}
    CheckContains -->|No| Throw[Throw BadHttpRequestException<br/>"The 'headerName' header is missing."]
    CheckContains -->|Yes| CallTry[Call TryGetHeaderValue<br/>(request, headerName)]
    CallTry --> GetRaw[rawValue = Headers[headerName]<br/>.FirstOrDefault()]
    GetRaw --> CheckNull{rawValue == null?}
    CheckNull -->|Yes| ReturnNull[Return null]
    CheckNull -->|No| Decode[Call UrlDecode(rawValue)]
    Decode --> ReturnDecoded[Return decoded value]
    Throw --> Propagate[Exception propagates to<br/>ASP.NET Core → 400 response]
    ReturnDecoded --> End([Return value])
    ReturnNull --> End
```

### TryGetHeaderValue (Optional Header)

```mermaid
flowchart TD
    Start([TryGetHeaderValue called]) --> GetRaw[rawValue = Headers[headerName]<br/>.FirstOrDefault()]
    GetRaw --> CheckNull{rawValue == null?}
    CheckNull -->|Yes| ReturnNull[Return null]
    CheckNull -->|No| Decode[Call UrlDecode(rawValue)]
    Decode --> ReturnDecoded[Return decoded value]
    ReturnNull --> End([Return null or value])
    ReturnDecoded --> End
```

### UrlDecode

```mermaid
flowchart TD
    Start([UrlDecode called]) --> CheckText{text null/whitespace<br/>OR no '%' character?}
    CheckText -->|Yes| ReturnText[Return text unchanged]
    CheckText -->|No| TryDecode[Try Uri.UnescapeDataString(text)]
    TryDecode --> CheckException{UriFormatException?}
    CheckException -->|No| ReturnDecoded[Return decoded string]
    CheckException -->|Yes| Catch[Return text unchanged]
    ReturnText --> End([Return string])
    ReturnDecoded --> End
    Catch --> End
```

## Step-by-Step Trace

### Scenario: Required Header Present

```
Request headers: X-Client-ID: client%2D123

1. GetHeaderValue(request, "X-Client-ID")
2. ContainsKey("X-Client-ID") → true
3. TryGetHeaderValue(request, "X-Client-ID")
4. rawValue = "client%2D123"
5. rawValue != null → UrlDecode("client%2D123")
6. Contains '%' → Uri.UnescapeDataString("client%2D123")
7. Returns "client-123"
8. GetHeaderValue returns "client-123"
```

### Scenario: Required Header Missing

```
Request headers: (no X-Client-ID)

1. GetHeaderValue(request, "X-Client-ID")
2. ContainsKey("X-Client-ID") → false
3. Throw BadHttpRequestException("The 'X-Client-ID' header is missing.")
4. ASP.NET Core catches → 400 Bad Request
5. Pipeline short-circuits (Next not called)
```

### Scenario: Optional Header Missing

```
Request headers: (no X-Request-ID)

1. TryGetHeaderValue(request, "X-Request-ID")
2. rawValue = Headers["X-Request-ID"].FirstOrDefault() → null
3. rawValue == null → return null
4. Consumer checks: if (requestId != null) { ... }
```

### Scenario: Malformed URL Encoding

```
Request headers: X-Custom: value%ZZinvalid

1. TryGetHeaderValue(request, "X-Custom")
2. rawValue = "value%ZZinvalid"
3. rawValue != null → UrlDecode("value%ZZinvalid")
4. Contains '%' → try Uri.UnescapeDataString("value%ZZinvalid")
5. Throws UriFormatException (invalid %ZZ sequence)
6. Catch → return "value%ZZinvalid" (original text)
7. TryGetHeaderValue returns "value%ZZinvalid"
```

## Error Handling Matrix

| Method | Missing Header | Null Value | Malformed URL Encoding | Empty String |
|--------|----------------|------------|------------------------|--------------|
| `GetHeaderValue` | `BadHttpRequestException` | N/A (ContainsKey guards) | Returns original text | Returns `""` |
| `TryGetHeaderValue` | Returns `null` | Returns `null` | Returns original text | Returns `""` |
| `UrlDecode` | N/A | Returns `null`/input | Returns original text | Returns `""` |

## Header Name Constants

Consumers should use `NuciApiHeaderNames` constants for standard headers:

```csharp
// Recommended
string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);

// Not recommended (typo-prone)
string clientId = GetHeaderValue(context.Request, "X-Client-ID");
```

| Constant | Value | Typical Usage |
|----------|-------|---------------|
| `ClientId` | `X-Client-ID` | Required — consumer identification |
| `RequestId` | `X-Request-ID` | Optional — correlation/tracing |
| `Timestamp` | `X-Timestamp` | Optional — request time |
| `HmacToken` | `X-Hmac-Token` | Required — signature verification |

## Interaction with ASP.NET Core Header Handling

### Case Insensitivity

`HttpRequest.Headers` is case-insensitive. `ContainsKey("X-Client-ID")` matches `x-client-id`, `X-CLIENT-ID`, etc.

### Multiple Values

`Headers[headerName].FirstOrDefault()` returns the **first** value if multiple are present. Subsequent values are ignored.

```
X-Custom: value1
X-Custom: value2

→ FirstOrDefault() returns "value1"
```

### StringValues Type

`Headers[headerName]` returns `StringValues` (a struct). `.FirstOrDefault()` on empty `StringValues` returns `null`.

## Testing Coverage

Unit tests in `NuciApiMiddlewareTests`:

| Test | Method | Scenario | Expected |
|------|--------|----------|----------|
| `Given_HeaderPresent_When_GetHeaderValue_Then_ReturnsDecodedValue` | `GetHeaderValue` | Header present with URL-encoded value | Decoded string |
| `Given_HeaderMissing_When_GetHeaderValue_Then_ThrowsBadHttpRequestException` | `GetHeaderValue` | Header absent | `BadHttpRequestException` |
| `Given_HeaderPresent_When_TryGetHeaderValue_Then_ReturnsDecodedValue` | `TryGetHeaderValue` | Header present | Decoded string |
| `Given_HeaderMissing_When_TryGetHeaderValue_Then_ReturnsNull` | `TryGetHeaderValue` | Header absent | `null` |
| `Given_ValidUrlEncodedText_When_UrlDecode_Then_ReturnsDecodedText` | `UrlDecode` | Valid `%XX` encoding | Decoded string |
| `Given_TextWithoutPercentSign_When_UrlDecode_Then_ReturnsOriginalText` | `UrlDecode` | No `%` in text | Original text |
| `Given_MalformedUrlEncodedText_When_UrlDecode_Then_ReturnsOriginalText` | `UrlDecode` | Invalid `%ZZ` sequence | Original text |

## Implementation Location

- **`GetHeaderValue`:** `NuciApiMiddleware.cs` lines 43-53
- **`TryGetHeaderValue`:** `NuciApiMiddleware.cs` lines 55-66
- **`UrlDecode`:** `NuciApiMiddleware.cs` lines 110-122
- **Header constants:** `NuciApiHeaderNames.cs` lines 10-21

## Related Flows

- [Middleware Invocation Flow](./middleware-invocation.md) — Where header access occurs in the pipeline
- [Client IP Extraction Flow](./client-ip-extraction.md) — Similar pattern for IP headers