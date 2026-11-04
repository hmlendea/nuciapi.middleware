[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/funding)
[![Latest Release](https://img.shields.io/github/v/release/hmlendea/nuciapi.middleware)](https://github.com/hmlendea/nuciapi.middleware/releases/latest)
[![Build Status](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/NuciAPI.Middleware)](https://nuget.org/packages/NuciAPI.Middleware)
[![License](https://img.shields.io/github/license/hmlendea/nuciapi.middleware)](https://github.com/hmlendea/nuciapi.middleware/blob/master/LICENSE)

# NuciAPI.Middleware

ASP.NET Core middleware base utilities for NuciAPI-based services.

## 📑 Table of Contents

- [Capabilities](#-capabilities)
- [Usage](#-usage)
- [Installation](#-installation)
  - [Package Manager Installation](#package-manager-installation)
- [Privacy and Data](#-privacy-and-data)
- [Development](#️-development)
  - [Requirements](#requirements)
  - [Setup](#setup)
  - [Build](#build)
  - [Test](#test)
  - [Release](#release)
- [GitHub Actions](#-github-actions)
- [Project Structure](#-project-structure)
  - [Projects and Packages](#projects-and-packages)
  - [Directories](#directories)
- [Architecture](#️-architecture)
- [Documentation](#-documentation)
- [Troubleshooting](#-troubleshooting)
- [FAQ](#-faq)
- [Contributing](#-contributing)
- [Code of Conduct](#-code-of-conduct)
- [Security](#-security)
- [Project Engagement](#-project-engagement)
- [License](#-license)

## ✨ Capabilities

- **Abstract middleware base class** — `NuciApiMiddleware` supplies common helpers for custom middleware implementations.
- **Required header access** — `GetHeaderValue` throws `BadHttpRequestException` when a header is missing.
- **Optional header access** — `TryGetHeaderValue` returns `null` when a header is absent and URL-decodes the value when present.
- **Client IP extraction** — `GetClientIpAddress` parses common forwarding headers (`X-Forwarded-For`, `Forwarded`, `X-Real-IP`, `CF-Connecting-IP`, `True-Client-IP`) with IPv4/IPv6 normalisation and falls back to the remote connection IP.
- **Hostname resolution** — `GetClientHostname` performs reverse DNS lookup via `NuciAPI.NetworkUtils.GetHostnames`.
- **Header name constants** — `NuciApiHeaderNames` provides static strings for `X-Client-ID`, `X-HMAC`, `X-Request-ID`, and `X-Timestamp`.

## 🚀 Usage

Create a custom middleware using `NuciApiMiddleware`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware;

public sealed class MyApiMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
        string clientIp = GetClientIpAddress(context);

        // Add your middleware logic here.
        await Next(context);
    }
}
```

Register your middleware in the ASP.NET Core pipeline:

```csharp
var app = builder.Build();

app.UseMiddleware<MyApiMiddleware>();

app.MapControllers();
app.Run();
```

## 📦 Installation

[![Obtain it from NuGet](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/nuget.png)](https://nuget.org/packages/NuciAPI.Middleware)

### Package Manager Installation

```bash
dotnet add package NuciAPI.Middleware
```

Or, via the `Package Manager Console`:

```powershell
Install-Package NuciAPI.Middleware
```

## 🛡️ Privacy and Data

NuciAPI.Middleware does not collect, process, transmit, or store personal data.

For the detailed description of how the application handles privacy and personal data, see [PRIVACY.md](./PRIVACY.md).

## 🛠️ Development

### Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Setup

```bash
git clone https://github.com/hmlendea/nuciapi.middleware.git
cd nuciapi.middleware
dotnet restore
```

### Build

```bash
dotnet build NuciAPI.Middleware.slnx
```

### Test

```bash
dotnet test NuciAPI.Middleware.slnx
```

### Release

The repository includes `release.sh`, which delegates to the upstream deployment script used by the project maintainer.

```bash
bash ./release.sh 2.0.4
```

This script downloads and executes an external release helper from `https://raw.githubusercontent.com/hmlendea/deployment-scripts/master/release/dotnet/10.0.sh`.

**Note:** Piping into `bash` is an intensely controversial topic. Please review any external scripts before running them in your environment!

## ⚙️ GitHub Actions

| Workflow | Purpose | What it does |
|----------|---------|--------------|
| [ Build Status ](https://github.com/hmlendea/nuciapi.middleware/blob/master/.github/workflows/dotnet.yml) | Continuous integration | Builds and tests the solution on push and pull request |
| [ GitHub Release ](https://github.com/hmlendea/nuciapi.middleware/blob/master/.github/workflows/github-release.yml) | GitHub Release publication | Creates GitHub Releases when release tags are pushed |
| [ NuGet.org Release ](https://github.com/hmlendea/nuciapi.middleware/blob/master/.github/workflows/nuget-release.yml) | NuGet package publication | Publishes NuGet packages to NuGet.org on release |

## 🗂️ Project Structure

ASP.NET Core middleware base library solution consisting of core, unit test, and architecture test projects.

### Projects and Packages

| Project | Type | Purpose |
|---------|------|---------|
| `NuciAPI.Middleware/NuciAPI.Middleware.csproj` | Library | Core middleware base utilities and header name constants |
| `NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj` | Unit Tests | Unit tests for middleware helpers and header constants |
| `NuciAPI.Middleware.ArchitectureTests/NuciAPI.Middleware.ArchitectureTests.csproj` | Architecture Tests | Architecture fitness tests enforcing design conventions |

### Directories

| Directory | Purpose |
|-----------|---------|
| `NuciAPI.Middleware/` | Source code for the middleware library |
| `NuciAPI.Middleware.UnitTests/` | Unit test project and test double implementations |
| `NuciAPI.Middleware.ArchitectureTests/` | Architecture test suite and dependency rules |
| `docs/` | Repository documentation catalogue |

## 🏗️ Architecture

NuciAPI.Middleware provides abstract base middleware utilities for ASP.NET Core applications.

See the [ARCHITECTURE.md](ARCHITECTURE.md) for the system context, principal components, runtime flows, ownership boundaries, dependencies, constraints, and extension points.

## 📚 Documentation

Comprehensive repository documentation is available in the `docs/` directory.

| Resource | Description |
|----------|-------------|
| [Quick Start](./docs/quick-start.md) | Getting started guide for new developers |
| [Architecture Overview](./docs/architecture.md) | Technical design and architectural concepts |
| [API Reference](./docs/api-reference/INDEX.md) | Complete public API reference |
| [Build & Deployment](./docs/build-and-deployment.md) | Building, packaging, and publishing procedures |
| [Testing Guide](./docs/testing.md) | Unit and architecture testing strategies |

## 🩺 Troubleshooting

See [Troubleshooting Guide](./docs/troubleshooting.md) for diagnostic procedures and resolutions to recurring problems.

## ❓ FAQ

See the [Frequently Asked Questions](./docs/faq.md) for responses to frequently raised inquiries.

## 🤝 Contributing

You are welcome to submit any suggestion, feedback, or modification to this project.

When doing so, please:
- Maintain cross-platform compatibility
- Preserve the existing public contract unless a breaking change is intentional
- Submit focused pull requests that conform to the existing code style
- Maintain your branch synchronised with `master`
- Revise the documentation when functionality changes
- Properly test all modifications, including edge cases and error conditions
- Include tests for additional or modified functionality
- Raise a new [issue](https://github.com/hmlendea/nuciapi.middleware/issues) for problems or suggestions

See the [contributing guidelines](./CONTRIBUTING.md) for details on how to report issues, suggest enhancements, and submit changes.

## 📜 Code of Conduct

See the [Code of Conduct](CODE_OF_CONDUCT.md) for the standards of conduct expected in all project spaces.

## 🔒 Security

NuciAPI.Middleware provides shared middleware helpers.

For information on reporting security vulnerabilities, see the [Security Policy](./SECURITY.md).

## 💝 Project Engagement

Discovered a problem or have a suggestion? [Open an issue](https://github.com/hmlendea/nuciapi.middleware/issues)!

If you find this project useful, consider [funding it](https://hmlendea.go.ro/funding) or starring ⭐️ it on GitHub!

[![Donate](https://raw.githubusercontent.com/hmlendea/readme-assets/master/donate_generic.png)](https://hmlendea.go.ro/funding)

## 📄 License

This project is being distributed under the `GNU General Public License v3.0 or later`.
See [LICENSE](./LICENSE) for additional information.
