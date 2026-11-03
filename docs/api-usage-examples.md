# API Usage Examples

This document provides practical examples for using NuciAPI.Middleware in common scenarios.

## Basic Middleware Implementation

### Minimal Secure Middleware

```csharp
// MyMiddleware.cs
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware;

public sealed class MySecureMiddleware(RequestDelegate next, IHmacValidator validator) 
    : NuciApiMiddleware(next)
{
    private readonly IHmacValidator _validator = validator;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        // 1. Read required headers (throw 400 if missing)
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        string hmac = GetHeaderValue(context.Request, NuciApiHeaderNames.HmacToken);
        string timestamp = GetHeaderValue(context.Request, NuciApiHeaderNames.Timestamp);
        
        // 2. Validate timestamp (replay protection)
        if (!DateTimeOffset.TryParse(timestamp, out var requestTime) ||
            Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid or expired timestamp");
            return;
        }
        
        // 3. Validate HMAC
        if (!_validator.Validate(clientId, hmac, context.Request, timestamp))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid signature");
            return;
        }
        
        // 4. Optional: Read correlation ID
        string requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId);
        if (requestId != null)
        {
            context.Items["CorrelationId"] = requestId;
            context.Response.Headers["X-Request-ID"] = requestId;
        }
        
        // 5. Continue pipeline
        await Next(context);
    }
}
```

### Registration in Program.cs

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for correct IP extraction
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Your load balancer IP
});

// Register services
builder.Services.AddSingleton<IHmacValidator, HmacValidator>();
builder.Services.AddSingleton<IClientRegistry, ClientRegistry>();

var app = builder.Build();

// Order matters!
app.UseForwardedHeaders();        // Must be BEFORE custom middleware
app.UseMiddleware<MySecureMiddleware>();
app.MapControllers();

app.Run();
```

## HMAC Validator Implementation

```csharp
// IHmacValidator.cs
public interface IHmacValidator
{
    bool Validate(string clientId, string hmac, HttpRequest request, string timestamp);
}

// HmacValidator.cs
using System.Security.Cryptography;
using System.Text;

public sealed class HmacValidator : IHmacValidator
{
    private readonly IClientRegistry _clientRegistry;
    
    public HmacValidator(IClientRegistry clientRegistry)
    {
        _clientRegistry = clientRegistry;
    }
    
    public bool Validate(string clientId, string providedHmac, HttpRequest request, string timestamp)
    {
        // 1. Look up client secret
        if (!_clientRegistry.TryGetSecret(clientId, out var secretBase64))
        {
            return false; // Don't reveal if client exists
        }
        
        var secret = Convert.FromBase64String(secretBase64);
        
        // 2. Build canonical request
        var canonical = BuildCanonicalRequest(request, timestamp);
        
        // 3. Compute expected HMAC
        using var hmac = new HMACSHA256(secret);
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var expectedHmac = Convert.ToBase64String(expectedBytes);
        
        // 4. Constant-time comparison
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedHmac),
            Encoding.UTF8.GetBytes(expectedHmac));
    }
    
    private string BuildCanonicalRequest(HttpRequest request, string timestamp)
    {
        // Method\nPath\nQueryString\nTimestamp\nBodyHash
        var bodyHash = ComputeBodyHash(request);
        return $"{request.Method}\n{request.Path}\n{request.QueryString}\n{timestamp}\n{bodyHash}";
    }
    
    private string ComputeBodyHash(HttpRequest request)
    {
        if (request.ContentLength == 0 || request.ContentLength == null)
            return "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"; // SHA256("")
        
        request.EnableBuffering();
        request.Body.Position = 0;
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(request.Body);
        request.Body.Position = 0;
        return Convert.ToBase64String(hash);
    }
}
```

## Client Registry

```csharp
// IClientRegistry.cs
public interface IClientRegistry
{
    bool TryGetSecret(string clientId, out string secretBase64);
    bool IsActive(string clientId);
}

// InMemoryClientRegistry.cs (for testing/simple cases)
public sealed class InMemoryClientRegistry : IClientRegistry
{
    private readonly Dictionary<string, Client> _clients = new()
    {
        ["client-123"] = new Client 
        { 
            Id = "client-123", 
            SecretBase64 = "base64-secret-here", 
            IsActive = true 
        }
    };
    
    public bool TryGetSecret(string clientId, out string secretBase64)
    {
        if (_clients.TryGetValue(clientId, out var client) && client.IsActive)
        {
            secretBase64 = client.SecretBase64;
            return true;
        }
        secretBase64 = null;
        return false;
    }
    
    public bool IsActive(string clientId)
        => _clients.TryGetValue(clientId, out var client) && client.IsActive;
    
    private sealed class Client
    {
        public string Id { get; set; }
        public string SecretBase64 { get; set; }
        public bool IsActive { get; set; }
    }
}
```

## Client-Side Request Signing

```csharp
// Client example (console app, service, etc.)
public sealed class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly byte[] _secret;
    
    public ApiClient(HttpClient httpClient, string clientId, string secretBase64)
    {
        _httpClient = httpClient;
        _clientId = clientId;
        _secret = Convert.FromBase64String(secretBase64);
    }
    
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var bodyHash = await ComputeBodyHash(request);
        var canonical = BuildCanonicalRequest(request, timestamp, bodyHash);
        var hmac = ComputeHmac(canonical);
        
        request.Headers.Add("X-Client-ID", _clientId);
        request.Headers.Add("X-Hmac-Token", hmac);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("N"));
        
        return await _httpClient.SendAsync(request);
    }
    
    private string ComputeHmac(string canonical)
    {
        using var hmacAlg = new HMACSHA256(_secret);
        var hash = hmacAlg.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(hash);
    }
    
    private static string BuildCanonicalRequest(HttpRequestMessage request, string timestamp, string bodyHash)
        => $"{request.Method}\n{request.RequestUri?.AbsolutePath}\n{request.RequestUri?.Query}\n{timestamp}\n{bodyHash}";
    
    private static async Task<string> ComputeBodyHash(HttpRequestMessage request)
    {
        if (request.Content == null)
            return "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        
        var bytes = await request.Content.ReadAsByteArrayAsync();
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
```

## Custom IP Extraction

```csharp
// CustomIpMiddleware.cs
public sealed class CustomIpMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    protected override string GetClientIpAddress(HttpContext context)
    {
        // 1. Check custom header first (e.g., from specific CDN)
        if (context.Request.Headers.TryGetValue("X-Custom-Client-IP", out var customIp))
        {
            if (IPAddress.TryParse(customIp, out var parsed))
                return parsed.ToString();
        }
        
        // 2. Check for specific proxy header
        if (context.Request.Headers.TryGetValue("X-My-Proxy-IP", out var proxyIp))
        {
            if (IPAddress.TryParse(proxyIp, out var parsed))
                return parsed.ToString();
        }
        
        // 3. Fall back to base implementation (standard headers)
        return base.GetClientIpAddress(context);
    }
    
    public override async Task InvokeAsync(HttpContext context)
    {
        var clientIp = GetClientIpAddress(context);
        
        // Use for geo-blocking, rate limiting, logging
        if (clientIp != null && _geoBlocker.IsBlocked(clientIp))
        {
            context.Response.StatusCode = 403;
            return;
        }
        
        await Next(context);
    }
}
```

## Rate Limiting Middleware

```csharp
// RateLimitingMiddleware.cs
public sealed class RateLimitingMiddleware(
    RequestDelegate next, 
    IRateLimiter rateLimiter) : NuciApiMiddleware(next)
{
    private readonly IRateLimiter _rateLimiter = rateLimiter;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        string clientId = GetHeaderValue(context.Request, NuciApiHeaderNames.ClientId);
        string clientIp = GetClientIpAddress(context);
        
        // Rate limit by client ID (primary) or IP (fallback)
        var key = clientId ?? $"ip:{clientIp}";
        
        if (!await _rateLimiter.TryAcquireAsync(key, 100, TimeSpan.FromMinutes(1)))
        {
            context.Response.StatusCode = 429;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }
        
        await Next(context);
    }
}

// IRateLimiter.cs
public interface IRateLimiter
{
    Task<bool> TryAcquireAsync(string key, int limit, TimeSpan window);
}
```

## Logging Middleware

```csharp
// LoggingMiddleware.cs
public sealed class LoggingMiddleware(
    RequestDelegate next, 
    ILogger<LoggingMiddleware> logger) : NuciApiMiddleware(next)
{
    private readonly ILogger<LoggingMiddleware> _logger = logger;
    
    public override async Task InvokeAsync(HttpContext context)
    {
        var requestId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.RequestId) 
            ?? Guid.NewGuid().ToString("N");
        var clientId = TryGetHeaderValue(context.Request, NuciApiHeaderNames.ClientId) ?? "anonymous";
        var clientIp = GetClientIpAddress(context);
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["ClientId"] = clientId,
            ["ClientIp"] = clientIp ?? "unknown",
            ["Path"] = context.Request.Path.Value ?? "/",
            ["Method"] = context.Request.Method
        });
        
        context.Response.Headers["X-Request-ID"] = requestId;
        
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Request started");
        
        try
        {
            await Next(context);
            _logger.LogInformation("Request completed in {ElapsedMs}ms with status {StatusCode}", 
                stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

## Complete Program.cs Example

```csharp
// Program.cs - Complete example
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Load balancer
});

// 2. Register services
builder.Services.AddSingleton<IHmacValidator, HmacValidator>();
builder.Services.AddSingleton<IClientRegistry, InMemoryClientRegistry>();
builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();

// 3. Add logging
builder.Logging.AddConsole();
builder.Logging.AddFilter("MyApp.Middleware", LogLevel.Information);

var app = builder.Build();

// 4. Pipeline order matters!
app.UseForwardedHeaders();                    // 1st: Process proxy headers
app.UseMiddleware<LoggingMiddleware>();       // 2nd: Log everything
app.UseMiddleware<RateLimitingMiddleware>();  // 3rd: Rate limit
app.UseMiddleware<MySecureMiddleware>();      // 4th: Authenticate

app.MapControllers();

app.Run();
```

## Testing Examples

```csharp
// Unit test for middleware
[Test]
public async Task Given_ValidHmac_When_InvokeAsync_Then_CallsNext()
{
    // Arrange
    var mockValidator = new Mock<IHmacValidator>();
    mockValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpRequest>(), It.IsAny<string>()))
        .Returns(true);
    
    var middleware = new MySecureMiddleware(_ => Task.CompletedTask, mockValidator.Object);
    var context = new DefaultHttpContext();
    context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-123";
    context.Request.Headers[NuciApiHeaderNames.HmacToken] = "valid-hmac";
    context.Request.Headers[NuciApiHeaderNames.Timestamp] = DateTimeOffset.UtcNow.ToString("O");
    
    // Act
    await middleware.InvokeAsync(context);
    
    // Assert
    Assert.That(context.Response.StatusCode, Is.EqualTo(200));
    mockValidator.Verify(v => v.Validate("client-123", "valid-hmac", context.Request, It.IsAny<string>()), Times.Once);
}
```

## Related Documents

- [Middleware Invocation Flow](flows/middleware-invocation.md) — Pipeline integration
- [Header Access Flow](flows/header-access.md) — Reading headers
- [Client IP Extraction Flow](flows/client-ip-extraction.md) — IP extraction
- [Security](security.md) — HMAC and security patterns
- [Testing](testing.md) — Test strategies