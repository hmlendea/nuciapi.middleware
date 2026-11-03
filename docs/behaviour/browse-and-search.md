# Browse and Search

This document describes how consumers discover and explore NuciAPI.Middleware capabilities.

## Discovering the Library

### NuGet Package

```bash
# Search NuGet
dotnet search NuciAPI.Middleware

# Or browse online
# https://www.nuget.org/packages/NuciAPI.Middleware
```

### GitHub Repository

- **Source:** https://github.com/hmlendea/nuciapi.middleware
- **Releases:** https://github.com/hmlendea/nuciapi.middleware/releases
- **Issues:** https://github.com/hmlendea/nuciapi.middleware/issues

## API Surface Discovery

### Primary Types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `NuciApiMiddleware` | `NuciAPI.Middleware` | Abstract base class for custom middleware |
| `NuciApiHeaderNames` | `NuciAPI.Middleware` | Header name constants |

### IDE Integration

- **IntelliSense:** Full XML documentation on all public/protected members
- **Go to Definition:** Navigate to source (embedded via SourceLink)
- **Parameter Hints:** See parameter names and types inline

### Documentation

| Document | Purpose |
|----------|---------|
| [API Reference: NuciApiMiddleware](../api-reference/nuci-api-middleware.md) | Complete member reference |
| [API Reference: NuciApiHeaderNames](../api-reference/nuci-api-header-names.md) | Header constants reference |
| [Components: Presentation](../components/presentation.md) | Public API surface overview |
| [API Usage Examples](../api-usage-examples.md) | Practical code examples |

## Searching for Solutions

### Common Tasks

| Task | Starting Point |
|------|----------------|
| Read a required header | `GetHeaderValue(request, NuciApiHeaderNames.ClientId)` |
| Read an optional header | `TryGetHeaderValue(request, NuciApiHeaderNames.RequestId)` |
| Get client IP | `GetClientIpAddress(context)` |
| Get client hostnames | `GetClientHostname(context)` |
| URL-decode a value | `UrlDecode(encodedString)` |
| Implement HMAC auth | [Security](../security.md#hmac-validation-pattern) |
| Add rate limiting | [API Usage Examples](../api-usage-examples.md#rate-limiting-middleware) |
| Add logging | [API Usage Examples](../api-usage-examples.md#logging-middleware) |

### Troubleshooting Search

| Symptom | Check |
|---------|-------|
| 400 Bad Request on header read | [Error Handling](../error-handling.md#base-class-error-behavior) |
| IP shows load balancer | [Client IP Extraction](../flows/client-ip-extraction.md#fallback-behavior) |
| HMAC validation fails | [Security](../security.md#hmac-validation-pattern) |
| DNS lookup timeout | [Troubleshooting](../troubleshooting.md#dns-lookup-timeout) |
| Middleware not executing | [Troubleshooting](../troubleshooting.md#middleware-not-executing) |

## Version Discovery

### Installed Version

```bash
# In project
dotnet list package NuciAPI.Middleware

# In solution
dotnet list NuciAPI.Middleware.sln package --include-transitive
```

### Available Versions

```bash
# NuGet CLI
dotnet nuget list source NuciAPI.Middleware --source nuget.org

# Or browse: https://www.nuget.org/packages/NuciAPI.Middleware/versions
```

## Related Documents

- [Quick Start](../quick-start.md) — Get running in 5 minutes
- [API Reference Index](../api-reference/INDEX.md) — All API reference links
- [Troubleshooting](../troubleshooting.md) — Common issues