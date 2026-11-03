# Build and Deployment

This document describes building, testing, packaging, and deploying NuciAPI.Middleware.

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0.100+ | Build, test, pack |
| Git | 2.40+ | Version control |
| NuGet CLI | 6.0+ | Package operations (optional) |

## Local Development

### Clone and Restore

```bash
git clone https://github.com/hmlendea/nuciapi.middleware.git
cd nuciapi.middleware
dotnet restore NuciAPI.Middleware.sln
```

### Build

```bash
# Debug build
dotnet build NuciAPI.Middleware.sln --configuration Debug

# Release build
dotnet build NuciAPI.Middleware.sln --configuration Release
```

### Run Tests

```bash
# All tests
dotnet test NuciAPI.Middleware.sln --configuration Release --no-build

# With coverage
dotnet test NuciAPI.Middleware.sln --configuration Release --no-build \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

### Format Code

```bash
# Check formatting
dotnet format NuciAPI.Middleware.sln --verify-no-changes

# Apply formatting
dotnet format NuciAPI.Middleware.sln
```

## Solution Structure

```
NuciAPI.Middleware.sln
├── NuciAPI.Middleware/                 # Main library
│   └── NuciAPI.Middleware.csproj       # Targets net10.0
└── NuciAPI.Middleware.UnitTests/       # Unit tests
    └── NuciAPI.Middleware.UnitTests.csproj
```

### Project Configuration (NuciAPI.Middleware.csproj)

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

## CI/CD Pipeline (GitHub Actions)

### Workflow: dotnet.yml (Build & Test)

**Trigger:** Push/PR to `master`

```yaml
# .github/workflows/dotnet.yml
name: .NET Build and Test

on:
  push:
    branches: [master]
  pull_request:
    branches: [master]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore
        run: dotnet restore NuciAPI.Middleware.sln
      - name: Build
        run: dotnet build NuciAPI.Middleware.sln --configuration Release --no-restore
      - name: Test
        run: dotnet test NuciAPI.Middleware.sln --configuration Release --no-build --verbosity normal
      - name: Format Check
        run: dotnet format NuciAPI.Middleware.sln --verify-no-changes
```

### Workflow: github-release.yml (NuGet Publish)

**Trigger:** GitHub Release published

```yaml
# .github/workflows/github-release.yml
name: NuGet Release

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore
        run: dotnet restore NuciAPI.Middleware.sln
      - name: Pack
        run: dotnet pack NuciAPI.Middleware/NuciAPI.Middleware.csproj --configuration Release --no-restore -o ./artifacts
      - name: Publish to NuGet.org
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
      - name: Publish Symbols
        run: dotnet nuget push ./artifacts/*.snupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

## Versioning

### Scheme: Semantic Versioning (SemVer)

| Version | When |
|---------|------|
| `MAJOR.MINOR.PATCH` | Standard |
| `MAJOR.MINOR.PATCH-preview.N` | Pre-release |

### Version Location

- **Source of truth:** `NuciAPI.Middleware.csproj` → `<PackageVersion>`
- **Git tag:** `v{version}` (e.g., `v1.2.3`)
- **GitHub Release:** Same tag

### Version Bump Process

```bash
# 1. Update PackageVersion in csproj
# 2. Commit: "chore: bump version to 1.2.3"
# 3. Tag: git tag v1.2.3
# 4. Push: git push origin master --tags
# 5. Create GitHub Release from tag → triggers NuGet publish
```

## Packaging

### Create Package Locally

```bash
dotnet pack NuciAPI.Middleware/NuciAPI.Middleware.csproj \
  --configuration Release \
  --output ./artifacts
```

### Package Contents

```
NuciAPI.Middleware.1.0.0.nupkg
├── lib/net10.0/
│   ├── NuciAPI.Middleware.dll
│   ├── NuciAPI.Middleware.xml        # Documentation
│   └── NuciAPI.Middleware.pdb        # Portable PDB
├── NuciAPI.Middleware.1.0.0.symbols.nupkg
│   └── lib/net10.0/
│       └── NuciAPI.Middleware.pdb    # Full symbols
```

### Verify Package

```bash
# List contents
dotnet nuget list package NuciAPI.Middleware --source ./artifacts

# Install locally for testing
dotnet add test-project package NuciAPI.Middleware --source ./artifacts --version 1.0.0
```

## Deployment Targets

### NuGet.org (Primary)

- **Package ID:** `NuciAPI.Middleware`
- **URL:** https://www.nuget.org/packages/NuciAPI.Middleware
- **Install:** `dotnet add package NuciAPI.Middleware`

### GitHub Packages (Secondary)

```yaml
# Alternative publish step
- name: Publish to GitHub Packages
  run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.GITHUB_TOKEN }} --source https://nuget.pkg.github.com/hmlendea/index.json
```

### Local/Private Feeds

```bash
# Azure Artifacts, MyGet, BaGet, etc.
dotnet nuget push ./artifacts/*.nupkg --api-key <key> --source <feed-url>
```

## Dependency Management

### Direct Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.AspNetCore.App` | Framework | ASP.NET Core APIs |
| `NuciAPI` | 3.6.0 | Network utilities (GetHostnames) |

### Transitive Dependencies

`NuciAPI` 3.6.0 has minimal dependencies. Check with:

```bash
dotnet list NuciAPI.Middleware/NuciAPI.Middleware.csproj package --include-transitive
```

### Vulnerability Scanning

```bash
# Built-in
dotnet list NuciAPI.Middleware/NuciAPI.Middleware.csproj package --vulnerable

# With NuGet audit
dotnet nuget audit NuciAPI.Middleware.sln
```

## Release Checklist

- [ ] All tests pass (`dotnet test`)
- [ ] Code formatted (`dotnet format --verify-no-changes`)
- [ ] No vulnerable dependencies (`dotnet list package --vulnerable`)
- [ ] Version bumped in `NuciAPI.Middleware.csproj`
- [ ] CHANGELOG.md updated (if exists)
- [ ] Commit and tag: `git tag v1.2.3`
- [ ] Push tag: `git push origin v1.2.3`
- [ ] Create GitHub Release from tag
- [ ] Verify NuGet package published
- [ ] Verify GitHub Release artifacts

## Troubleshooting Build Issues

### SDK Version Mismatch

```bash
# Check installed SDKs
dotnet --list-sdks

# Use global.json to pin version
echo '{"sdk": {"version": "10.0.113"}}' > global.json
```

### Restore Failures

```bash
# Clear caches
dotnet nuget locals all --clear
dotnet restore --force
```

### Test Discovery Issues

```bash
# Explicit test project
dotnet test NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj
```

## Related Documents

- [Testing](testing.md) — Test strategies
- [Configuration](./configuration.md) — Build-time configuration
- [Security](./security.md) — Supply chain security
- [Repository Structure](repository-structure.md) — File layout