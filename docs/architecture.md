# Architecture (Documentation Complement)

This document complements the root [ARCHITECTURE.md](../ARCHITECTURE.md) with additional implementation detail and cross-references to other documentation files.

## Purpose

The root ARCHITECTURE.md provides the authoritative architectural description. This file elaborates on specific areas with implementation-level detail, links to component deep-dives, and traces execution flows to source locations.

## System Context Elaboration

The library boundary is the `NuciAPI.Middleware` assembly (`NuciAPI.Middleware.dll`). Consumers are ASP.NET Core applications that:

1. Reference the NuGet package
2. Create a class inheriting from `NuciApiMiddleware`
3. Implement `InvokeAsync(HttpContext)`
4. Register via `app.UseMiddleware<YourMiddleware>()`

The library has no independent process, no configuration, no persistent state, and no direct I/O.

## Architectural Style Detail

**Library/SDK Profile** — Single assembly, minimal public surface:
- 1 abstract base class: `NuciApiMiddleware`
- 1 static constants class: `NuciApiHeaderNames`
- All implementation details are `private` or `protected`

This style enforces:
- **Extension by inheritance only** — Consumers cannot compose or wrap; they must inherit
- **No configuration surface** — Behavior is fixed; customization requires overriding protected methods
- **Version coupling** — Public API changes require major version bump (GPL-3.0-or-later)

## Component Boundaries

| Component | File | Public Surface | Internal Detail |
|-----------|------|----------------|-----------------|
| NuciApiMiddleware | `NuciAPI.Middleware/NuciApiMiddleware.cs` | Constructor, `InvokeAsync`, 5 protected helpers | 4 private IP parsing methods, hardcoded header array |
| NuciApiHeaderNames | `NuciAPI.Middleware/NuciApiHeaderNames.cs` | 4 string constants | None |

See [components/presentation.md](./components/presentation.md) for full API surface documentation.

## Runtime Flow Detail

The root ARCHITECTURE.md shows the sequence diagram. Key implementation traces:

1. **Middleware registration** → `app.UseMiddleware<T>()` → ASP.NET Core instantiates `T` per request (or singleton for stateless)
2. **InvokeAsync entry** → Derived middleware's override → calls base helpers as needed
3. **Header access** → `GetHeaderValue`/`TryGetHeaderValue` → reads `HttpRequest.Headers` → `UrlDecode` on value
4. **IP extraction** → `GetClientIpAddress` → iterates `ClientIpForwardingHeaderNames` → parses each candidate → `TryNormalizeIpAddress` → validates via `IPAddress.TryParse` → falls back to `HttpContext.Connection.RemoteIpAddress`
5. **Hostname resolution** → `GetClientHostname` → `NetworkUtils.GetHostnames(ip)` (NuciAPI package)
6. **Pipeline continuation** → `await Next(context)`

See [flows/middleware-invocation.md](./flows/middleware-invocation.md), [flows/client-ip-extraction.md](./flows/client-ip-extraction.md), [flows/header-access.md](./flows/header-access.md) for detailed traces.

## Data Flow Detail

No persistent data. In-flight transformations only:

```
HttpRequest.Headers (string dictionary)
    → GetHeaderValue/TryGetHeaderValue
    → UrlDecode (Uri.UnescapeDataString, try/catch)
    → string (decoded or original on failure)

Forwarding headers (X-Forwarded-For, Forwarded, etc.)
    → GetClientIpAddress
    → Split by ',' or ';'
    → TryNormalizeIpAddress (trim, unquote, bracket removal, port stripping, "unknown" filter)
    → IPAddress.TryParse
    → string (normalized IP) or continue to next header
    → Fallback: HttpContext.Connection.RemoteIpAddress?.ToString()

Normalized IP
    → GetClientHostname
    → NetworkUtils.GetHostnames (NuciAPI)
    → List<string> (empty on failure)
```

See [data-model.md](./data-model.md) for data representation details.

## Dependency Direction Enforcement

```
Consumer Application
    → NuciApiMiddleware (inherit)
    → NuciApiHeaderNames (reference)
        → Microsoft.AspNetCore.App (framework reference)
        → NuciAPI (package reference, v3.6.0)
        → NuciWeb.HTTP (package reference, v1.7.2, transitive)
```

**Prohibited**: Consumer depending on `private`/`internal` members of `NuciApiMiddleware` (IP parsing, header normalization internals).

See [dependencies.md](./dependencies.md) for full dependency graph.

## Extension Points

Only two extension mechanisms exist:

1. **Inherit `NuciApiMiddleware`** — Implement `InvokeAsync`, use protected helpers
2. **Override `GetClientIpAddress`** — Custom forwarding header logic

No interfaces, no DI registration points, no events, no plugins.

See [components/application-services.md](./components/application-services.md) for helper contracts.

## Design Constraints (Implementation View)

| Constraint | Source | Impact |
|------------|--------|--------|
| Hardcoded `ClientIpForwardingHeaderNames` | `NuciApiMiddleware.cs` line 14 | Cannot customize without inheritance |
| No `IOptions`/configuration | Project design | Avoids `Microsoft.Extensions.Configuration` dependency |
| Single TFM `net10.0` | `.csproj` | No multi-targeting for older ASP.NET Core |
| GPL-3.0-or-later | `.csproj` `PackageLicenseExpression` | Viral copyleft for consumers |
| Abstract base only | `NuciApiMiddleware.cs` | No ready-to-use middleware |

See [design-decisions.md](./design-decisions.md) for rationale.

## Testing Architecture

Two projects in solution:
- `NuciAPI.Middleware` — library
- `NuciAPI.Middleware.UnitTests` — NUnit tests

Test doubles: `TestNuciApiMiddleware` exposes protected helpers for testing.

Coverage: Constructor, header access, IP extraction (X-Forwarded-For, Forwarded IPv6, fallback), URL decode.

Gaps: No integration tests with real host, no `GetClientHostname` tests (external dependency), no concurrency tests.

See [testing.md](./testing.md) for full test strategy.

## Source Map (Implementation)

| Area | Path |
|------|------|
| Library root | `NuciAPI.Middleware/` |
| Base middleware | `NuciAPI.Middleware/NuciApiMiddleware.cs` |
| Header constants | `NuciAPI.Middleware/NuciApiHeaderNames.cs` |
| Project file | `NuciAPI.Middleware/NuciAPI.Middleware.csproj` |
| Assembly visibility | `NuciAPI.Middleware/AssemblyInfo.cs` |
| Unit tests | `NuciAPI.Middleware.UnitTests/` |
| Test doubles | `NuciAPI.Middleware.UnitTests/TestDoubles/TestNuciApiMiddleware.cs` |
| Solution | `NuciAPI.Middleware.sln` |
| CI/CD | `.github/workflows/dotnet.yml`, `.github/workflows/github-release.yml` |