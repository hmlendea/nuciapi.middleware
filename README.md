[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/fund.html) [![Latest GitHub release](https://img.shields.io/github/v/release/hmlendea/nuciapi.middleware)](https://github.com/hmlendea/nuciapi.middleware/releases/latest) [![Build Status](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml) [![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://gnu.org/licenses/gpl-3.0)

# NuciAPI.Middleware

Middleware components for ASP.NET Core APIs, focused on:

- unified exception-to-response mapping
- request logging with useful API metadata
- request header validation
- replay attack protection (nonce/timestamp based)
- scanner and probing request protection

## Installation

[![Get it from NuGet](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/nuget.png)](https://nuget.org/packages/NuciAPI.Middleware)

### .NET CLI

```bash
dotnet add package NuciAPI.Middleware
```

### Package Manager

```powershell
Install-Package NuciAPI.Middleware
```

## Requirements

- .NET (target framework: `net10.0`)
- ASP.NET Core

## Quick Start

Configure services:

```csharp
using NuciAPI.Middleware;
using NuciLog.Core;

var builder = WebApplication.CreateBuilder(args);

// Required by scanner and replay protection middleware
builder.Services.AddNuciApiScannerProtection();
builder.Services.AddNuciApiReplayProtection();

// Required only if request logging middleware is used
// Register your ILogger implementation from NuciLog.Core
builder.Services.AddSingleton<ILogger, MyLogger>();
```

Configure pipeline:

```csharp
var app = builder.Build();

app.UseNuciApiExceptionHandling();
app.UseNuciApiRequestLogging();
app.UseNuciApiHeaderValidation();
app.UseNuciApiReplayProtection();
app.UseNuciApiScannerProtection();

app.MapControllers();
app.Run();
```

## Middleware Overview

### Exception Handling

`UseNuciApiExceptionHandling()` catches downstream exceptions and writes a JSON API error response.

Main mappings include:

- `BadHttpRequestException`, `FormatException`, `ArgumentException`, `ValidationException` -> `400`
- `SecurityException`, `UnauthorizedAccessException` -> `403`
- `HttpRequestException`, `TaskCanceledException`, `TimeoutException` -> `503`
- `AuthenticationException` -> `401`
- `KeyNotFoundException` -> `404`
- `RequestAlreadyProcessedException` -> `409`
- `NotImplementedException` -> `501`
- `OperationCanceledException` -> `499`
- fallback -> `500`

### Request Logging

`UseNuciApiRequestLogging()` logs request start and completion/failure using `NuciLog.Core.ILogger`.

Captured metadata includes:

- method
- path
- query string
- client IP (supports common forwarding headers)
- `X-Client-ID`
- `X-Request-ID`
- `X-Timestamp`
- `X-HMAC`
- response status code
- elapsed milliseconds

### Header Validation

`UseNuciApiHeaderValidation()` validates required headers:

- `X-Client-ID` (must be longer than 3 characters)
- `X-Request-ID` (must be uppercase GUID)
- `X-Timestamp` (must be parseable as `DateTimeOffset`)

Invalid or missing values result in `BadHttpRequestException`.

### Replay Protection

`UseNuciApiReplayProtection()` prevents duplicate request processing based on:

- `X-Client-ID`
- `X-Request-ID`
- request path

Behavior:

- accepts timestamps within +/- 5 minutes skew from current UTC time
- stores nonce entries in memory for 5 minutes
- throws `RequestAlreadyProcessedException` (`409` when exception handling middleware is enabled)

### Scanner Protection

`UseNuciApiScannerProtection()` blocks scanner/probing traffic and bans source IPs in memory.

Behavior:

- blocks known malicious/suspicious resource patterns
- blocks suspicious query patterns
- blocks certain scanner `From` headers
- bans offending IP for 10 hours
- returns `403` for blocked requests

Client IP extraction supports `X-Forwarded-For`, `Forwarded`, `X-Real-IP`, `CF-Connecting-IP`, and `True-Client-IP`.

## Header Contract

When using header validation and replay protection, clients should send:

- `X-Client-ID`: stable client identifier (length > 3)
- `X-Request-ID`: unique uppercase GUID per request
- `X-Timestamp`: request timestamp in an ISO-8601 format (for example generated with `DateTimeOffset.UtcNow.ToString("O")`)

Example:

```http
X-Client-ID: MY-SERVICE
X-Request-ID: 8A6C35CC-9D5C-4AA9-B5E8-7A09F9EBDDF5
X-Timestamp: 2026-04-20T10:22:31.1791268+00:00
```

## Recommended Pipeline Order

Recommended order for predictable behavior:

1. `UseNuciApiExceptionHandling()`
2. `UseNuciApiRequestLogging()`
3. `UseNuciApiHeaderValidation()`
4. `UseNuciApiReplayProtection()`
5. `UseNuciApiScannerProtection()`

This places error handling first, validates input before replay checks, and applies scanner blocking before controllers.

## Notes

- Replay and scanner protection rely on in-memory cache. In multi-instance deployments, use sticky routing or replace with a distributed strategy if global deduplication/banning is required.
- Scanner protection is intentionally strict and may block generic root probes. Test against your expected traffic patterns.

## Development

### Prerequisites

- .NET SDK compatible with the target framework

### Build

```bash
dotnet build NuciAPI.Middleware.csproj
```

### Run

```bash
dotnet run --project NuciAPI.Middleware.csproj
```

### Test

```bash
dotnet test
```

## Contributing

Contributions are welcome.

When contributing:

- keep the project cross-platform
- preserve the existing public API unless a breaking change is intentional
- keep the changes focused and consistent with the current coding style
- update the documentation when the behavior changes
- include tests for any new behavior

## License

Licensed under the GNU General Public License v3.0 or later.
See [LICENSE](./LICENSE) for details.
