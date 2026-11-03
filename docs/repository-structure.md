# Repository Structure

This document describes the file and directory structure of the NuciAPI.Middleware repository.

## Root Directory

```
nuciapi.middleware/
├── NuciAPI.Middleware/                 # Main library project
├── NuciAPI.Middleware.UnitTests/       # Unit test project
├── docs/                               # Documentation
├── .github/workflows/                  # GitHub Actions CI/CD
├── NuciAPI.Middleware.sln              # Solution file
├── README.md                           # Project overview
├── LICENSE                             # GPL-3.0-or-later
├── SECURITY.md                         # Vulnerability reporting
├── CONTRIBUTING.md                     # Contribution guide
├── CODE_OF_CONDUCT.md                  # Community standards
├── PRIVACY.md                          # Data handling
├── ARCHITECTURE.md                     # Architecture documentation
└── ROADMAP.md                          # Future plans
```

## Main Library (NuciAPI.Middleware/)

```
NuciAPI.Middleware/
├── NuciApiMiddleware.cs                # Abstract base middleware class
├── NuciApiHeaderNames.cs               # Header name constants
├── AssemblyInfo.cs                     # Assembly attributes
├── NuciAPI.Middleware.csproj           # Project configuration
├── bin/                                # Build output (gitignored)
└── obj/                                # Build intermediates (gitignored)
```

### Key Files

| File | Purpose |
|------|---------|
| `NuciApiMiddleware.cs` | Abstract base class with protected helpers |
| `NuciApiHeaderNames.cs` | Static class with header constants |
| `NuciAPI.Middleware.csproj` | Targets net10.0, references Microsoft.AspNetCore.App + NuciAPI 3.6.0 |

## Unit Tests (NuciAPI.Middleware.UnitTests/)

```
NuciAPI.Middleware.UnitTests/
├── NuciApiMiddlewareTests.cs         # Tests for base class helpers
├── NuciApiHeaderNamesTests.cs        # Tests for header constants
├── NuciAPI.Middleware.UnitTests.csproj
├── TestDoubles/
│   └── TestNuciApiMiddleware.cs      # Test double exposing protected members
├── bin/                              # Build output (gitignored)
└── obj/                              # Build intermediates (gitignored)
```

### Test Double Pattern

`TestNuciApiMiddleware` inherits from `NuciApiMiddleware` and exposes protected methods as public for unit testing without requiring ASP.NET Core TestHost.

## Documentation (docs/)

```
docs/
├── INDEX.md                          # Master documentation index
├── architecture.md                   # Architecture overview
├── configuration.md                  # Configuration guide
├── error-handling.md                 # Error handling patterns
├── logging.md                        # Logging practices
├── security.md                       # Security considerations
├── concurrency-and-scheduling.md     # Concurrency model
├── state-and-persistence.md          # State management
├── build-and-deployment.md           # Build and release process
├── testing.md                        # Testing strategies
├── troubleshooting.md                # Common issues and solutions
├── change-guide.md                   # Versioning and migration
├── documentation-maintenance.md      # Documentation maintenance
├── ambiguities-and-open-questions.md # Known ambiguities
├── api-usage-examples.md             # Practical examples
├── repository-overview.md            # High-level overview
├── repository-structure.md           # This file
├── api-reference/
│   ├── INDEX.md                      # API reference index
│   ├── nuci-api-middleware.md        # NuciApiMiddleware reference
│   └── nuci-api-header-names.md      # NuciApiHeaderNames reference
├── components/
│   ├── presentation.md               # Public API surface
│   ├── host-and-composition.md       # ASP.NET Core integration
│   └── application-services.md       # Helper method contracts
└── flows/
    ├── middleware-invocation.md      # Pipeline execution flow
    ├── client-ip-extraction.md       # IP extraction details
    └── header-access.md              # Header reading details
```

## CI/CD (.github/workflows/)

```
.github/workflows/
├── dotnet.yml                        # Build and test on push/PR
└── github-release.yml                # NuGet publish on release
```

### Workflow Triggers

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `dotnet.yml` | Push/PR to `master` | Restore, build, test, format check |
| `github-release.yml` | GitHub Release published | Pack and publish to NuGet.org |

## Solution File

```xml
<!-- NuciAPI.Middleware.sln -->
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NuciAPI.Middleware", "NuciAPI.Middleware/NuciAPI.Middleware.csproj", "{GUID}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NuciAPI.Middleware.UnitTests", "NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj", "{GUID}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {GUID}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {GUID}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {GUID}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {GUID}.Release|Any CPU.Build.0 = Release|Any CPU
        {GUID2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {GUID2}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {GUID2}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {GUID2}.Release|Any CPU.Build.0 = Release|Any CPU
    EndGlobalSection
EndGlobal
```

## Project Configuration

### NuciAPI.Middleware.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>NuciAPI.Middleware</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <Authors>hmlendea</Authors>
    <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/hmlendea/nuciapi.middleware</PackageProjectUrl>
    <RepositoryUrl>https://github.com/hmlendea/nuciapi.middleware</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="NuciAPI" Version="3.6.0" />
  </ItemGroup>
</Project>
```

### NuciAPI.Middleware.UnitTests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\NuciAPI.Middleware\NuciAPI.Middleware.csproj" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  </ItemGroup>
</Project>
```

## Git Ignore Patterns

```
# Build outputs
bin/
obj/

# IDE
.vs/
*.user
*.suo

# OS
.DS_Store
Thumbs.db

# NuGet
*.nupkg
*.snupkg

# Test results
TestResults/
*.trx
coverage/

# Documentation (generated)
docs/api/ (if generated)
```

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Projects | PascalCase, namespace-aligned | `NuciAPI.Middleware` |
| Source files | PascalCase, one type per file | `NuciApiMiddleware.cs` |
| Test files | `{Type}Tests.cs` | `NuciApiMiddlewareTests.cs` |
| Test doubles | `Test{Type}.cs` | `TestNuciApiMiddleware.cs` |
| Documentation | kebab-case.md | `middleware-invocation.md` |
| Namespaces | PascalCase, matches project | `NuciAPI.Middleware` |

## Related Documents

- [Repository Overview](repository-overview.md) — High-level overview
- [Build and Deployment](build-and-deployment.md) — Build process
- [Testing](testing.md) — Test structure
- [Documentation Maintenance](documentation-maintenance.md) — Docs structure