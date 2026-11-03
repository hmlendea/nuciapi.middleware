# Repository Overview

This document provides a high-level overview of the NuciAPI.Middleware repository.

## Purpose

NuciAPI.Middleware is an ASP.NET Core middleware base utilities library that provides common functionality for building secure, observable middleware components.

## Key Features

| Feature | Description |
|---------|-------------|
| **Header Access** | Typed helpers for reading required/optional headers with URL decoding |
| **Client IP Extraction** | Multi-header IP extraction with proxy support (X-Forwarded-For, Forwarded, etc.) |
| **Hostname Resolution** | Reverse DNS lookup via NuciAPI package |
| **Base Class** | Abstract `NuciApiMiddleware` with protected helpers for derived classes |
| **Constants** | Standard header names (`X-Client-ID`, `X-Hmac-Token`, etc.) |

## Target Audience

- **Library Consumers** — Developers building custom ASP.NET Core middleware
- **Security Engineers** — Implementing HMAC-based authentication
- **Platform Teams** — Building middleware pipelines for microservices

## Technology Stack

| Component | Version |
|-----------|---------|
| Target Framework | .NET 10.0 (net10.0) |
| ASP.NET Core | Via `Microsoft.AspNetCore.App` framework reference |
| NuciAPI Dependency | 3.6.0 (for `NetworkUtils.GetHostnames`) |
| Testing | NUnit 4.x, Moq |
| Packaging | NuGet (NuciAPI.Middleware) |

## Repository Structure

```
nuciapi.middleware/
├── NuciAPI.Middleware/              # Main library
│   ├── NuciApiMiddleware.cs         # Abstract base class
│   ├── NuciApiHeaderNames.cs        # Header constants
│   └── NuciAPI.Middleware.csproj    # Project file
├── NuciAPI.Middleware.UnitTests/    # Unit tests
│   ├── NuciApiMiddlewareTests.cs    # Base class tests
│   ├── NuciApiHeaderNamesTests.cs   # Constants tests
│   └── TestDoubles/
│       └── TestNuciApiMiddleware.cs # Test double
├── docs/                            # Documentation
├── .github/workflows/               # CI/CD
├── NuciAPI.Middleware.sln           # Solution file
├── README.md                        # Project overview
├── LICENSE                          # GPL-3.0-or-later
├── SECURITY.md                      # Vulnerability reporting
├── CONTRIBUTING.md                  # Contribution guide
├── CODE_OF_CONDUCT.md               # Community standards
├── PRIVACY.md                       # Data handling
├── ARCHITECTURE.md                  # Architecture documentation
└── ROADMAP.md                       # Future plans
```

## Quick Start

```bash
# Install
dotnet add package NuciAPI.Middleware

# Use in middleware
public sealed class MyMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        await Next(context);
    }
}
```

## License

GNU General Public License v3.0 or later — see [LICENSE](../LICENSE).

## Links

- **NuGet:** https://www.nuget.org/packages/NuciAPI.Middleware
- **Source:** https://github.com/hmlendea/nuciapi.middleware
- **Issues:** https://github.com/hmlendea/nuciapi.middleware/issues
- **Security:** [SECURITY.md](../SECURITY.md)

## Related Documents

- [Architecture](architecture.md) — Technical architecture
- [Components: Presentation](components/presentation.md) — Public API surface
- [Build and Deployment](build-and-deployment.md) — CI/CD process
- [Testing](testing.md) — Test strategies