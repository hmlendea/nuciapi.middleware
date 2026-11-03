# Dependencies

This document describes the external dependencies of NuciAPI.Middleware.

## Runtime Dependencies

### Direct Dependencies

| Package | Version | Purpose | License |
|---------|---------|---------|---------|
| `Microsoft.AspNetCore.App` | Framework reference | ASP.NET Core base types (`HttpContext`, `HttpRequest`, `RequestDelegate`, etc.) | Apache-2.0 |
| `NuciAPI` | 3.6.0 | `NetworkUtils.GetHostnames` for reverse DNS lookup | GPL-3.0-or-later |

### Transitive Dependencies (NuciAPI 3.6.0)

NuciAPI brings its own dependencies. Key transitive dependencies include:

| Package | Purpose |
|---------|---------|
| `System.Net.Http` | HTTP client infrastructure |
| `System.Text.Json` | JSON serialization |
| `Microsoft.Extensions.Logging` | Logging abstractions |
| `Microsoft.Extensions.Options` | Options pattern |

Full transitive dependency tree available via:
```bash
dotnet list NuciAPI.Middleware/NuciAPI.Middleware.csproj package --include-transitive
```

## Development Dependencies

### Test Project Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `NUnit` | 4.2.2 | Unit test framework |
| `NUnit3TestAdapter` | 4.6.0 | VS Test Platform adapter |
| `Moq` | 4.20.72 | Mocking framework |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | Test SDK |

### Build Tooling

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0.1xx | Build, test, pack |
| `dotnet-format` | Built-in | Code formatting |

## Dependency Graph

```
NuciAPI.Middleware
├── Microsoft.AspNetCore.App (framework)
└── NuciAPI 3.6.0
    ├── System.Net.Http
    ├── System.Text.Json
    ├── Microsoft.Extensions.Logging
    ├── Microsoft.Extensions.Options
    └── (other NuciAPI dependencies)

NuciAPI.Middleware.UnitTests
├── NuciAPI.Middleware (project reference)
├── NUnit
├── NUnit3TestAdapter
├── Moq
└── Microsoft.NET.Test.Sdk
```

## Version Policy

| Dependency Type | Policy |
|-----------------|--------|
| Framework references | Track .NET LTS/STS releases |
| NuciAPI | Minor version pinning (3.6.x), test before upgrading |
| Test packages | Latest stable at release time |
| Build tooling | .NET SDK version in `global.json` |

## Security

- **NuGet package validation**: All packages verified via SHA512
- **Supply chain**: Dependencies sourced from nuget.org only
- **Vulnerability scanning**: GitHub Dependabot enabled
- **License compliance**: All dependencies compatible with GPL-3.0-or-later

### License Compatibility

| Dependency | License | Compatible with GPL-3.0? |
|------------|---------|--------------------------|
| Microsoft.AspNetCore.App | Apache-2.0 | Yes |
| NuciAPI | GPL-3.0-or-later | Yes (same license) |
| NUnit | MIT | Yes |
| Moq | BSD-3-Clause | Yes |
| System.* packages | MIT | Yes |

## Updating Dependencies

### NuciAPI Update Process

1. Check NuciAPI changelog for breaking changes
2. Update version in `NuciAPI.Middleware.csproj`
3. Run full test suite: `dotnet test NuciAPI.Middleware.sln`
4. Verify `GetClientHostname` behavior unchanged
5. Update `CHANGELOG.md` if behavior changes

### Test Dependencies

Update before each release:
```bash
dotnet add NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj package NUnit --version latest
dotnet add NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj package Moq --version latest
```

## Related Documents

- [Build and Deployment](build-and-deployment.md) — Dependency management in CI
- [Security](security.md) — Supply chain security
- [Architecture](architecture.md) — Dependency direction