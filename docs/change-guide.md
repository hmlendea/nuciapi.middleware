# Change Guide

This document describes how to manage changes to NuciAPI.Middleware, including versioning, breaking changes, and migration guidance.

## Versioning Policy

### Semantic Versioning (SemVer)

| Version Component | Incremented When |
|-------------------|------------------|
| **MAJOR** | Breaking changes to public API |
| **MINOR** | New features, backward compatible |
| **PATCH** | Bug fixes, backward compatible |

### Release Types

| Type | Version Format | Stability |
|------|----------------|-----------|
| Stable | `1.2.3` | Production ready |
| Preview | `1.2.3-preview.1` | Pre-release, may change |
| Release Candidate | `1.2.3-rc.1` | Feature complete, testing |

## Public API Surface

### What Constitutes Public API

| Category | Included | Stability Guarantee |
|----------|----------|---------------------|
| `NuciApiMiddleware` class | ✅ | SemVer |
| `NuciApiMiddleware` constructor | ✅ | SemVer |
| `Next` property | ✅ | SemVer |
| `InvokeAsync` (abstract) | ✅ | SemVer |
| `GetHeaderValue` | ✅ | SemVer |
| `TryGetHeaderValue` | ✅ | SemVer |
| `GetClientIpAddress` | ✅ | SemVer |
| `GetClientHostname` | ✅ | SemVer |
| `UrlDecode` | ✅ | SemVer |
| `NuciApiHeaderNames` constants | ✅ | SemVer |
| Private methods | ❌ | Can change anytime |
| Internal implementation | ❌ | Can change anytime |

### What Can Change in MINOR/PATCH

- ✅ Adding new protected virtual methods
- ✅ Adding new constants to `NuciApiHeaderNames`
- ✅ Performance improvements
- ✅ Bug fixes
- ✅ New overloads (with default parameters)
- ✅ Additional XML documentation

### What Requires MAJOR

- ❌ Removing/renaming public/protected members
- ❌ Changing method signatures
- ❌ Changing exception types thrown
- ❌ Changing header constant values
- ❌ Changing `InvokeAsync` contract
- ❌ Removing `virtual` from overridable methods

## Breaking Change Process

### 1. Identify Breaking Change

```csharp
// Example: Changing GetClientIpAddress signature
// BREAKING: protected string GetClientIpAddress(HttpContext context)
// TO:       protected ValueTask<string> GetClientIpAddressAsync(HttpContext context)
```

### 2. Deprecation Period (MINOR Release)

```csharp
// v1.2.0 - Add new method, mark old as obsolete
[Obsolete("Use GetClientIpAddressAsync instead. Will be removed in v2.0.")]
protected string GetClientIpAddress(HttpContext context) { ... }

protected virtual ValueTask<string> GetClientIpAddressAsync(HttpContext context) { ... }
```

### 3. Removal (MAJOR Release)

```csharp
// v2.0.0 - Remove obsolete member
// protected string GetClientIpAddress(HttpContext context) { ... } // REMOVED
```

## Migration Guides

### From 1.x to 2.0 (Hypothetical)

**Breaking Changes:**
- `GetClientIpAddress` → `GetClientIpAddressAsync` (returns `ValueTask<string>`)
- `GetClientHostname` → `GetClientHostnameAsync` (returns `ValueTask<List<string>>`)
- `UrlDecode` made `async` (unlikely but possible)

**Migration Steps:**
```csharp
// Before (v1.x)
public override async Task InvokeAsync(HttpContext context)
{
    string ip = GetClientIpAddress(context);
    var hostnames = GetClientHostname(context);
    await Next(context);
}

// After (v2.0)
public override async Task InvokeAsync(HttpContext context)
{
    string ip = await GetClientIpAddressAsync(context);
    var hostnames = await GetClientHostnameAsync(context);
    await Next(context);
}
```

### Header Constant Changes

If header names must change (rare):

```csharp
// v1.x
public const string ClientId = "X-Client-ID";

// v2.0 - New constant, old marked obsolete
[Obsolete("Use ClientIdV2. Will be removed in v3.0.")]
public const string ClientId = "X-Client-ID";

public const string ClientIdV2 = "X-Nuci-Client-ID";
```

## Deprecation Policy

### Obsolete Attribute Usage

```csharp
[Obsolete("Use NewMethod instead. Will be removed in v{MAJOR+1}.0.", error: false)]
protected string OldMethod() { ... }

// For immediate breaking change in next MAJOR:
[Obsolete("Removed in v2.0. Use NewMethod.", error: true)]
protected string RemovedMethod() { ... }
```

### Deprecation Timeline

| Phase | Version | Duration | Action |
|-------|---------|----------|--------|
| Announce | MINOR | 1 release | Add `[Obsolete]`, document alternative |
| Warn | MINOR+1 | 1 release | Keep `[Obsolete]`, ensure alternative works |
| Remove | MAJOR | Next MAJOR | Remove member, bump MAJOR version |

## Change Log Format

### CHANGELOG.md Structure

```markdown
# Changelog

All notable changes documented per [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]
### Added
- New feature X

### Changed
- Behavior of Y

### Deprecated
- OldMethod (use NewMethod)

### Removed
- RemovedMethod

### Fixed
- Bug in Z

### Security
- Vulnerability fix

## [1.2.0] - 2025-01-15
### Added
- GetClientIpAddressAsync method
- Support for X-Custom-Forwarded-For header

### Deprecated
- GetClientIpAddress (use GetClientIpAddressAsync)

## [1.1.1] - 2025-01-10
### Fixed
- IP normalization for IPv6 with port

## [1.1.0] - 2025-01-05
### Added
- NuciApiHeaderNames.CustomHeader constant
```

## Consumer Communication

### NuGet Package Metadata

```xml
<!-- In .csproj for pre-release -->
<PackageVersion>1.2.0-preview.1</PackageVersion>
<PackageReleaseNotes>
Preview release with async IP extraction.
Breaking changes planned for 2.0: GetClientIpAddress → GetClientIpAddressAsync.
</PackageReleaseNotes>
```

### GitHub Release Notes

```markdown
## NuciAPI.Middleware 1.2.0-preview.1

### New Features
- Async IP extraction: `GetClientIpAddressAsync`
- New header constant: `X-Custom-Forwarded-For`

### Deprecations
- `GetClientIpAddress` marked obsolete, will be removed in 2.0

### Migration
See [Change Guide](https://github.com/hmlendea/nuciapi.middleware/blob/master/docs/change-guide.md#from-1x-to-20-hypothetical)
```

## Testing Changes

### Compatibility Tests

```csharp
// Test that derived middleware still compiles and works
[Test]
public void Given_DerivedMiddleware_When_Compiled_Then_NoObsoleteWarnings()
{
    // Compile test project referencing NuciAPI.Middleware
    // Verify no CS0618 (obsolete) warnings treated as errors
}

[Test]
public async Task Given_OldApi_When_Used_Then_StillWorks()
{
    var middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);
    var context = new DefaultHttpContext();
    context.Request.Headers["X-Forwarded-For"] = "198.51.100.10";
    
    // Old API should still work during deprecation period
    var ip = middleware.GetClientIp(context);
    
    Assert.That(ip, Is.EqualTo("198.51.100.10"));
}
```

## Release Checklist for Maintainers

- [ ] Update `PackageVersion` in `NuciAPI.Middleware.csproj`
- [ ] Update `CHANGELOG.md`
- [ ] Run full test suite: `dotnet test`
- [ ] Check for obsolete warnings: `dotnet build --warnaserror`
- [ ] Verify package: `dotnet pack`
- [ ] Create git tag: `git tag v1.2.0`
- [ ] Push tag: `git push origin v1.2.0`
- [ ] Create GitHub Release
- [ ] Verify NuGet publish
- [ ] Announce (if breaking changes)

## Related Documents

- [Build and Deployment](./build-and-deployment.md) — Release process
- [Versioning in Build and Deployment](./build-and-deployment.md#versioning)
- [API Reference](api-reference/INDEX.md) — Public API surface
- [Testing](testing.md) — Compatibility testing