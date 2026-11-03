# Security

This document describes security considerations for NuciAPI.Middleware and applications using it.

## Threat Model

### Assets Protected

| Asset | Threat | Mitigation |
|-------|--------|------------|
| Client credentials (HMAC secrets) | Disclosure in logs/memory | Never log secrets; use secure storage |
| Request integrity | Tampering in transit | HMAC validation in derived middleware |
| Client identity | Spoofing via headers | Validate HMAC; don't trust headers alone |
| IP-based access control | Header spoofing | Configure `ForwardedHeadersOptions` correctly |
| Correlation IDs | Injection | Validate/sanitize `X-Request-ID` if used in logs |

### Trust Boundaries

```
Internet → [CDN/WAF] → [Load Balancer] → [Kestrel/IIS] → [UseForwardedHeaders] → [NuciAPI Middleware] → [Application]
                │              │                    │                        │
                │              │                    │                        └─ Trusts headers HERE
                │              │                    └─ Adds/validates Forwarded headers
                │              └─ May add X-Forwarded-For
                └─ May add CF-Connecting-IP, True-Client-IP
```

**Critical:** The middleware trusts headers **after** `UseForwardedHeaders`. Configure trust at the ASP.NET Core level, not in this library.

## Required Security Configuration

### Forwarded Headers (Mandatory for IP Extraction)

```csharp
// Program.cs
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Add ONLY trusted proxy IPs/ranges
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Load balancer
    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8)); // VPC range
});

var app = builder.Build();
app.UseForwardedHeaders(); // BEFORE UseMiddleware
app.UseMiddleware<MyNuciApiMiddleware>();
```

Without this, `GetClientIpAddress` reads raw headers that clients can spoof.

### HTTPS Enforcement

```csharp
app.UseHttpsRedirection(); // Redirect HTTP → HTTPS
app.UseHsts(); // HSTS header (production)
```

## HMAC Validation Pattern

Derived middleware typically implements HMAC validation:

```csharp
public sealed class SecureMiddleware(RequestDelegate next, IHmacValidator validator)
    : NuciApiMiddleware(next)
{
    private readonly IHmacValidator _validator = validator;

    public override async Task InvokeAsync(HttpContext context)
    {
        // Required headers — throw 400 if missing
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
        string timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);

        // Replay protection: reject old timestamps
        if (!DateTimeOffset.TryParse(timestamp, out var requestTime) ||
            Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid timestamp");
            return;
        }

        // Validate HMAC
        if (!_validator.Validate(clientId, hmac, context.Request, timestamp))
        {
            context.Response.StatusCode = 401;
            return;
        }

        await Next(context);
    }
}
```

### HMAC Validator Interface

```csharp
public interface IHmacValidator
{
    bool Validate(string clientId, string hmac, HttpRequest request, string timestamp);
}
```

### Secure HMAC Implementation

```csharp
public sealed class HmacValidator : IHmacValidator
{
    private readonly IClientSecretStore _secrets;

    public bool Validate(string clientId, string hmac, HttpRequest request, string timestamp)
    {
        if (!_secrets.TryGetSecret(clientId, out var secret))
            return false; // Don't reveal if client exists

        // Reconstruct canonical request
        var canonical = BuildCanonicalRequest(request, timestamp);

        // Compute expected HMAC
        using var hmacAlg = new HMACSHA256(Convert.FromBase64String(secret));
        var expected = hmacAlg.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var expectedB64 = Convert.ToBase64String(expected);

        // Constant-time comparison
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hmac),
            Encoding.UTF8.GetBytes(expectedB64));
    }

    private string BuildCanonicalRequest(HttpRequest request, string timestamp)
    {
        // Method\nPath\nQueryString\nTimestamp\nBodyHash
        var bodyHash = ComputeBodyHash(request);
        return $"{request.Method}\n{request.Path}\n{request.QueryString}\n{timestamp}\n{bodyHash}";
    }
}
```

## Input Validation

### Header Value Sanitization

```csharp
public override async Task InvokeAsync(HttpContext context)
{
    string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);

    // Sanitize before using in logs, headers, or storage
    if (requestId != null)
    {
        // Allow only alphanumeric, hyphen, underscore
        if (!Regex.IsMatch(requestId, @"^[a-zA-Z0-9_-]{1,64}$"))
        {
            requestId = null; // Discard invalid
        }
        else
        {
            context.Response.Headers["X-Request-ID"] = requestId; // Safe to echo
        }
    }

    await Next(context);
}
```

### Request Body Limits

```csharp
// In Program.cs - before middleware
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    // Limit to 10 MB
    if (context.Request.ContentLength > 10_000_000)
    {
        context.Response.StatusCode = 413;
        return;
    }
    await next();
});

app.UseMiddleware<MyNuciApiMiddleware>();
```

## Secure Coding Practices

### Never Log Secrets

```csharp
// ❌ NEVER
_logger.LogDebug("HMAC: {Hmac}", hmac);
_logger.LogInformation("Client secret: {Secret}", secret);

// ✅ ALWAYS
_logger.LogDebug("HMAC validation {Result}", success ? "succeeded" : "failed");
_logger.LogInformation("Authenticating client {ClientId}", clientId);
```

### Constant-Time Comparisons

```csharp
// ❌ Vulnerable to timing attacks
return providedHmac == expectedHmac;

// ✅ Constant-time
return CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(providedHmac),
    Encoding.UTF8.GetBytes(expectedHmac));
```

### Secure Random Generation

```csharp
// For generating nonces, tokens, etc.
var nonce = RandomNumberGenerator.GetBytes(32);
var token = Convert.ToBase64String(nonce);
```

## Dependency Security

### NuGet Package Validation

```xml
<!-- NuciAPI.Middleware.csproj -->
<PackageReference Include="NuciAPI" Version="3.6.0" />
<!-- No other runtime dependencies -->
```

- Single dependency: `NuciAPI` (v3.6.0)
- No transitive dependencies with known vulnerabilities (as of library release)
- Target framework: `net10.0` (uses .NET 10 base libraries)

### Supply Chain

- Package published to NuGet.org from GitHub Actions
- Source code on GitHub: https://github.com/hmlendea/nuciapi.middleware
- Signed commits, verified releases
- SBOM available via `dotnet sbom`

## Vulnerability Reporting

See [SECURITY.md](../SECURITY.md) for coordinated disclosure process.

## Security Checklist for Consumers

- [ ] Configure `ForwardedHeadersOptions` with trusted proxies only
- [ ] Place `UseForwardedHeaders()` before `UseMiddleware`
- [ ] Implement HMAC validation with constant-time comparison
- [ ] Enforce timestamp freshness (replay protection)
- [ ] Never log HMAC tokens, secrets, or credentials
- [ ] Sanitize `X-Request-ID` before echoing or storing
- [ ] Set request body size limits
- [ ] Use HTTPS in production with HSTS
- [ ] Regularly update NuGet packages (`dotnet list package --vulnerable`)
- [ ] Monitor for security advisories on dependencies

## Related Documents

- [Configuration](./configuration.md) — Forwarded headers setup
- [Error Handling](./error-handling.md) — Secure error responses
- [Logging](./logging.md) — Sensitive data in logs
- [Client IP Extraction Flow](flows/client-ip-extraction.md) — Header trust
- [SECURITY.md](../SECURITY.md) — Vulnerability reporting