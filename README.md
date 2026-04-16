[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/fund.html) [![Latest GitHub release](https://img.shields.io/github/v/release/hmlendea/nuciapi.middleware)](https://github.com/hmlendea/nuciapi.middleware/releases/latest) [![Build Status](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml)

# Installation

[![Get it from NuGet](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/nuget.png)](https://nuget.org/packages/NuciAPI.Middleware)

**.NET CLI**:
```bash
dotnet add package NuciAPI.Middleware
```

**Package Manager**:
```powershell
Install-Package NuciAPI.Middleware
```

# Usage

```csharp
builder.Services.AddNuciApiScannerProtection();

app.UseNuciApiExceptionHandling();
app.UseNuciApiScannerProtection();
```

`UseNuciApiScannerProtection()` blocks requests for resources commonly targeted by scanners, and bans the caller IP address in the in-memory cache for 10 hours so that any subsequent requests from the same address are denied as well.
