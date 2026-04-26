[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/fund.html)
[![Latest Release](https://img.shields.io/github/v/release/hmlendea/nuciapi.middleware)](https://github.com/hmlendea/nuciapi.middleware/releases/latest)
[![Build Status](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/dotnet.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://gnu.org/licenses/gpl-3.0)

# NuciAPI.Middleware

ASP.NET Core middleware base utilities for NuciAPI-based services.

This package provides shared middleware helpers rather than a full set of ready-to-register middleware components.

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

- .NET SDK/runtime with support for `net10.0`
- ASP.NET Core (`Microsoft.AspNetCore.App`)

## What This Package Includes

### `NuciApiMiddleware` (abstract base class)

Base class for building custom middleware with common helpers:

- required header access with built-in `BadHttpRequestException` on missing values
- optional header access with URL-decoding
- client IP extraction from common forwarding headers:
	- `X-Forwarded-For`
	- `Forwarded`
	- `X-Real-IP`
	- `CF-Connecting-IP`
	- `True-Client-IP`
- fallback to `HttpContext.Connection.RemoteIpAddress`

### `NuciApiHeaderNames`

Static header name constants:

- `X-Client-ID`
- `X-HMAC`
- `X-Request-ID`
- `X-Timestamp`

## Usage Example

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

## Notes on Forwarded Client IPs

- Forwarding headers should be trusted only when your app is behind known reverse proxies/load balancers.
- If no valid forwarded value is found, the remote connection IP is used.
- The parser supports normalized IPv4/IPv6 extraction from the `Forwarded` header format.

## Development

### Build

```bash
dotnet build NuciAPI.Middleware.sln
```

### Test

```bash
dotnet test NuciAPI.Middleware.sln
```

## Contributing

Contributions are welcome.

Please:

- keep the changes cross-platform
- keep the pull requests focused and consistent with the existing style
- update the documentation when the behaviour changes
- add or update the tests for any new behaviour

## Related Projects

- [NuciAPI.Middleware](https://github.com/hmlendea/nuciapi.middleware)
- [NuciAPI.Middleware.ExceptionHandling](https://github.com/hmlendea/nuciapi.exceptionhandling)
- [NuciAPI.Middleware.Logging](https://github.com/hmlendea/nuciapi.logging)
- [NuciAPI.Middleware.Security](https://github.com/hmlendea/nuciapi.security)

## License

Licensed under the GNU General Public License v3.0 or later.
See [LICENSE](./LICENSE) for details.
