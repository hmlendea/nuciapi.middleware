# Testing

This document describes testing strategies, patterns, and tools for NuciAPI.Middleware and derived middleware.

## Test Project Structure

```
NuciAPI.Middleware.UnitTests/
├── NuciAPI.Middleware.UnitTests.csproj
├── NuciApiHeaderNamesTests.cs      # Tests for header constants
├── NuciApiMiddlewareTests.cs       # Tests for base class helpers
└── TestDoubles/
    └── TestNuciApiMiddleware.cs    # Test double exposing protected members
```

### Test Double Pattern

```csharp
// TestDoubles/TestNuciApiMiddleware.cs
public sealed class TestNuciApiMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
{
    // Expose protected helpers as public for testing
    public string GetHeader(HttpContext context, string headerName)
        => GetHeaderValue(context.Request, headerName);

    public string TryGetHeader(HttpContext context, string headerName)
        => TryGetHeaderValue(context.Request, headerName);

    public string GetClientIp(HttpContext context)
        => GetClientIpAddress(context);

    public string Decode(string text)
        => UrlDecode(text);

    // Minimal InvokeAsync for pipeline tests
    public override Task InvokeAsync(HttpContext context) => Next(context);
}
```

## Unit Test Framework

- **Framework:** NUnit 4.x
- **Assertions:** NUnit classic (`Assert.That`, `Is.EqualTo`, etc.)
- **Mocking:** Moq (for external dependencies)
- **Test Host:** `Microsoft.AspNetCore.TestHost` not used — pure unit tests with `DefaultHttpContext`

## Running Tests

```bash
# All tests
dotnet test NuciAPI.Middleware.sln --configuration Release

# Specific project
dotnet test NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj

# With filter
dotnet test --filter "FullyQualifiedName~GetClientIpAddress"

# Verbose output
dotnet test --verbosity normal
```

## Test Categories

### 1. Header Access Tests

```csharp
// NuciApiMiddlewareTests.cs
[TestFixture]
public class GetHeaderValueTests
{
    private TestNuciApiMiddleware _middleware;
    private DefaultHttpContext _context;

    [SetUp]
    public void SetUp()
    {
        _middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);
        _context = new DefaultHttpContext();
    }

    [Test]
    public void Given_HeaderPresent_When_GetHeaderValue_Then_ReturnsDecodedValue()
    {
        _context.Request.Headers["X-Custom"] = "value%20with%20spaces";

        var result = _middleware.GetHeader(_context, "X-Custom");

        Assert.That(result, Is.EqualTo("value with spaces"));
    }

    [Test]
    public void Given_HeaderMissing_When_GetHeaderValue_Then_ThrowsBadHttpRequestException()
    {
        var ex = Assert.Throws<BadHttpRequestException>(() =>
            _middleware.GetHeader(_context, "X-Missing"));

        Assert.That(ex.Message, Does.Contain("X-Missing"));
    }

    [Test]
    public void Given_HeaderPresent_When_TryGetHeaderValue_Then_ReturnsDecodedValue()
    {
        _context.Request.Headers["X-Custom"] = "value%20with%20spaces";

        var result = _middleware.TryGetHeader(_context, "X-Custom");

        Assert.That(result, Is.EqualTo("value with spaces"));
    }

    [Test]
    public void Given_HeaderMissing_When_TryGetHeaderValue_Then_ReturnsNull()
    {
        var result = _middleware.TryGetHeader(_context, "X-Missing");

        Assert.That(result, Is.Null);
    }
}
```

### 2. URL Decoding Tests

```csharp
[TestFixture]
public class UrlDecodeTests
{
    private TestNuciApiMiddleware _middleware;

    [SetUp]
    public void SetUp() => _middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);

    [Test]
    public void Given_ValidUrlEncodedText_When_UrlDecode_Then_ReturnsDecodedText()
    {
        var result = _middleware.Decode("hello%20world%21");
        Assert.That(result, Is.EqualTo("hello world!"));
    }

    [Test]
    public void Given_TextWithoutPercentSign_When_UrlDecode_Then_ReturnsOriginalText()
    {
        var result = _middleware.Decode("hello world");
        Assert.That(result, Is.EqualTo("hello world"));
    }

    [Test]
    public void Given_MalformedUrlEncodedText_When_UrlDecode_Then_ReturnsOriginalText()
    {
        var result = _middleware.Decode("hello%ZZworld");
        Assert.That(result, Is.EqualTo("hello%ZZworld"));
    }

    [Test]
    public void Given_NullOrEmpty_When_UrlDecode_Then_ReturnsInput()
    {
        Assert.That(_middleware.Decode(null), Is.Null);
        Assert.That(_middleware.Decode(""), Is.EqualTo(""));
        Assert.That(_middleware.Decode("   "), Is.EqualTo("   "));
    }
}
```

### 3. Client IP Extraction Tests

```csharp
[TestFixture]
public class GetClientIpAddressTests
{
    private TestNuciApiMiddleware _middleware;
    private DefaultHttpContext _context;

    [SetUp]
    public void SetUp()
    {
        _middleware = new TestNuciApiMiddleware(_ => Task.CompletedTask);
        _context = new DefaultHttpContext();
    }

    [Test]
    public void Given_XForwardedForHeader_When_GetClientIpAddress_Then_ReturnsFirstValidIp()
    {
        _context.Request.Headers["X-Forwarded-For"] = "unknown, 198.51.100.10, 198.51.100.20";

        var result = _middleware.GetClientIp(_context);

        Assert.That(result, Is.EqualTo("198.51.100.10"));
    }

    [Test]
    public void Given_ForwardedHeaderWithIpv6AndPort_When_GetClientIpAddress_Then_ReturnsNormalizedIp()
    {
        _context.Request.Headers["Forwarded"] = "for=unknown;proto=https, for=\"[2001:db8:cafe::17]:4711\";proto=https";

        var result = _middleware.GetClientIp(_context);

        Assert.That(result, Is.EqualTo("2001:db8:cafe::17"));
    }

    [Test]
    public void Given_NoForwardingHeaders_When_GetClientIpAddress_Then_ReturnsRemoteIpAddress()
    {
        _context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");

        var result = _middleware.GetClientIp(_context);

        Assert.That(result, Is.EqualTo("203.0.113.5"));
    }

    [Test]
    public void Given_CloudflareHeader_When_GetClientIpAddress_Then_ReturnsIp()
    {
        _context.Request.Headers["CF-Connecting-IP"] = "192.0.2.1";

        var result = _middleware.GetClientIp(_context);

        Assert.That(result, Is.EqualTo("192.0.2.1"));
    }
}
```

### 4. Header Constants Tests

```csharp
// NuciApiHeaderNamesTests.cs
[TestFixture]
public class NuciApiHeaderNamesTests
{
    [Test]
    public void ClientId_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.ClientId, Is.EqualTo("X-Client-ID"));
    }

    [Test]
    public void RequestId_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.RequestId, Is.EqualTo("X-Request-ID"));
    }

    [Test]
    public void Timestamp_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.Timestamp, Is.EqualTo("X-Timestamp"));
    }

    [Test]
    public void HmacToken_IsExpectedValue()
    {
        Assert.That(NuciApiHeaderNames.HmacToken, Is.EqualTo("X-Hmac-Token"));
    }
}
```

## Integration Testing Patterns

### Testing Full Pipeline with TestServer

```csharp
// For integration tests (separate project if needed)
public class MiddlewareIntegrationTests
{
    private TestServer _server;
    private HttpClient _client;

    [SetUp]
    public void SetUp()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHmacValidator, TestHmacValidator>();
            })
            .Configure(app =>
            {
                app.UseMiddleware<SecureMiddleware>();
                app.Run(async ctx => await ctx.Response.WriteAsync("OK"));
            });

        _server = new TestServer(builder);
        _client = _server.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Test]
    public async Task Given_ValidHmac_When_Request_Then_Returns200()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Client-ID", "client-1");
        request.Headers.Add("X-Hmac-Token", "valid-hmac");
        request.Headers.Add("X-Timestamp", DateTimeOffset.UtcNow.ToString("O"));

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

## Mocking External Dependencies

```csharp
// Using Moq for services
public class SecureMiddlewareTests
{
    private Mock<IHmacValidator> _mockValidator;
    private SecureMiddleware _middleware;
    private DefaultHttpContext _context;

    [SetUp]
    public void SetUp()
    {
        _mockValidator = new Mock<IHmacValidator>();
        _middleware = new SecureMiddleware(_ => Task.CompletedTask, _mockValidator.Object);
        _context = new DefaultHttpContext();
    }

    [Test]
    public async Task Given_ValidHmac_When_InvokeAsync_Then_CallsNext()
    {
        _context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-1";
        _context.Request.Headers[NuciApiHeaderNames.HmacToken] = "hmac";
        _context.Request.Headers[NuciApiHeaderNames.Timestamp] = DateTimeOffset.UtcNow.ToString("O");
        _mockValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpRequest>(), It.IsAny<string>()))
            .Returns(true);

        var nextCalled = false;
        _middleware = new SecureMiddleware(ctx => { nextCalled = true; return Task.CompletedTask; }, _mockValidator.Object);

        await _middleware.InvokeAsync(_context);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task Given_InvalidHmac_When_InvokeAsync_Then_Returns401()
    {
        _context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-1";
        _context.Request.Headers[NuciApiHeaderNames.HmacToken] = "bad-hmac";
        _context.Request.Headers[NuciApiHeaderNames.Timestamp] = DateTimeOffset.UtcNow.ToString("O");
        _mockValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpRequest>(), It.IsAny<string>()))
            .Returns(false);

        await _middleware.InvokeAsync(_context);

        Assert.That(_context.Response.StatusCode, Is.EqualTo(401));
    }
}
```

## Test Data Builders

```csharp
// Helper for creating test contexts
public static class HttpContextBuilder
{
    public static DefaultHttpContext Create(string clientId = null, string requestId = null, string ip = null)
    {
        var context = new DefaultHttpContext();

        if (clientId != null)
            context.Request.Headers[NuciApiHeaderNames.ClientId] = clientId;

        if (requestId != null)
            context.Request.Headers[NuciApiHeaderNames.RequestId] = requestId;

        if (ip != null)
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        return context;
    }

    public static DefaultHttpContext WithForwardedFor(this DefaultHttpContext context, string value)
    {
        context.Request.Headers["X-Forwarded-For"] = value;
        return context;
    }

    public static DefaultHttpContext WithForwarded(this DefaultHttpContext context, string value)
    {
        context.Request.Headers["Forwarded"] = value;
        return context;
    }
}

// Usage
var context = HttpContextBuilder.Create(clientId: "test-client")
    .WithForwardedFor("198.51.100.10, 198.51.100.20");
```

## Coverage Requirements

| Metric | Target |
|--------|--------|
| Line coverage | ≥ 90% |
| Branch coverage | ≥ 85% |
| Critical paths (IP extraction, HMAC) | 100% |

### Coverage Report

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
# Generates .cobertura.xml for CI integration
```

## Continuous Integration

Tests run on every push/PR via GitHub Actions:

```yaml
# .github/workflows/dotnet.yml
- name: Test
  run: dotnet test NuciAPI.Middleware.sln --configuration Release --no-build --verbosity normal
```

## Testing Checklist for Consumers

When implementing derived middleware, test:

- [ ] Required headers → 400 when missing
- [ ] Optional headers → null when missing
- [ ] Valid HMAC → calls Next
- [ ] Invalid HMAC → 401
- [ ] Replay protection (timestamp) → 401
- [ ] IP extraction with various headers
- [ ] IP extraction fallback to connection IP
- [ ] Custom header validation logic
- [ ] Rate limiting integration
- [ ] Logging at appropriate levels
- [ ] Error responses don't leak sensitive data
- [ ] Cancellation token handling

## Related Documents

- [Middleware Invocation Flow](flows/middleware-invocation.md) — What to test in pipeline
- [Error Handling](./error-handling.md) — Error response testing
- [Security](./security.md) — Security test cases
- [Build and Deployment](./build-and-deployment.md) — CI integration
- [Application Services](components/application-services.md) — Helper method contracts