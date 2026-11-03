# Troubleshooting

This document describes common issues, diagnostics, and solutions for NuciAPI.Middleware.

## Common Issues

### 1. "The 'X-Client-ID' header is missing" (400 Bad Request)

**Symptom:** Requests return 400 with message "The 'X-Client-ID' header is missing."

**Cause:** `GetHeaderValue` called for a header that isn't present.

**Diagnosis:**
```csharp
// Check what headers are actually received
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Headers: {Headers}",
        context.Request.Headers.Select(h => $"{h.Key}: {h.Value}"));
    await next();
});
```

**Solutions:**
- Ensure client sends required headers
- Check load balancer/proxy isn't stripping headers
- Verify header name matches exactly (case-insensitive but check spelling)

### 2. Client IP Always Shows Load Balancer IP

**Symptom:** `GetClientIpAddress` returns the load balancer's IP instead of the original client.

**Cause:** `ForwardedHeadersOptions` not configured or middleware ordering wrong.

**Diagnosis:**
```csharp
// Check what headers arrive at middleware
app.Use(async (context, next) =>
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    var forwarded = context.Request.Headers["Forwarded"].FirstOrDefault();
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    // Log these
    await next();
});
```

**Solutions:**
```csharp
// 1. Configure ForwardedHeadersOptions
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // Your LB IP
});

// 2. Order: UseForwardedHeaders BEFORE UseMiddleware
app.UseForwardedHeaders();
app.UseMiddleware<MyNuciApiMiddleware>();
```

### 3. HMAC Validation Fails for Valid Requests

**Symptom:** Valid requests return 401 Unauthorized.

**Causes & Solutions:**

| Cause | Diagnosis | Solution |
|-------|-----------|----------|
| Timestamp skew | Check server vs client time | Sync clocks (NTP); increase tolerance |
| Body hash mismatch | Log canonical request on both sides | Ensure identical body reading (buffering) |
| Header case sensitivity | Check header names | Use `NuciApiHeaderNames` constants |
| Secret mismatch | Verify client ID maps to correct secret | Check client registry |

**Debug canonical request:**
```csharp
// In HMAC validator, log for debugging (dev only)
_logger.LogDebug("Canonical: {Canonical}", canonicalRequest);
_logger.LogDebug("Expected HMAC: {Expected}", expectedHmac);
_logger.LogDebug("Provided HMAC: {Provided}", providedHmac);
```

### 4. "Invalid signature" on First Request After Deploy

**Symptom:** First request after deployment fails HMAC, subsequent work.

**Cause:** In-memory secret cache not warmed up, or key rotation.

**Solution:** Pre-load secrets on startup or use distributed cache with background refresh.

### 5. Middleware Not Executing

**Symptom:** Custom middleware logic never runs.

**Causes:**
- Registered after terminal middleware (Map, MapWhen, endpoints)
- Exception in earlier middleware short-circuits
- Wrong registration method

**Correct Registration:**
```csharp
// ✅ Correct - in pipeline before endpoints
app.UseMiddleware<MyMiddleware>();
app.MapControllers(); // or app.UseEndpoints(...)

// ❌ Wrong - after endpoints
app.MapControllers();
app.UseMiddleware<MyMiddleware>(); // Never executes for mapped routes
```

### 6. DNS Lookup Timeout in GetClientHostname

**Symptom:** Requests hang or timeout when calling `GetClientHostname`.

**Cause:** Reverse DNS lookup blocks on slow/failing DNS.

**Solutions:**
- Don't call `GetClientHostname` in hot path
- Cache results (see [State and Persistence](./state-and-persistence.md))
- Set DNS timeout via `DnsClient` or custom resolver
- Make it async with timeout:

```csharp
public async Task<List<string>> GetClientHostnameAsync(HttpContext context, TimeSpan timeout)
{
    var ip = GetClientIpAddress(context);
    if (string.IsNullOrEmpty(ip)) return [];

    using var cts = new CancellationTokenSource(timeout);
    try
    {
        return await Task.Run(() => NetworkUtils.GetHostnames(ip), cts.Token);
    }
    catch (OperationCanceledException)
    {
        return []; // Timeout
    }
}
```

### 7. Memory Leak / High Memory Usage

**Symptom:** Memory grows over time.

**Causes:**
- Storing request data in static/instance fields
- Not disposing `HttpClient` or other `IDisposable`
- Caching without expiration

**Diagnosis:**
```bash
# Capture memory dump
dotnet-dump collect --process-id <pid>

# Analyze
dotnet-dump analyze <dump-file>
> dumpheap -stat
> dumpheap -type <suspected-type>
```

### 8. Build Fails: "Framework 'Microsoft.AspNetCore.App' not found"

**Cause:** .NET SDK version mismatch or missing workload.

**Solution:**
```bash
# Install ASP.NET Core workload
dotnet workload install aspnetcore

# Or use correct SDK version (check global.json)
dotnet --list-sdks
```

## Diagnostic Tools

### Enable Detailed Logging

```csharp
// Program.cs
builder.Logging.AddFilter("NuciAPI.Middleware", LogLevel.Trace);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Debug);
```

### Request Inspection Middleware

```csharp
// Temporary diagnostic middleware (remove in production)
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("=== REQUEST START ===");
    logger.LogInformation("Method: {Method}", context.Request.Method);
    logger.LogInformation("Path: {Path}", context.Request.Path);
    logger.LogInformation("Headers: {Headers}",
        string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}")));
    logger.LogInformation("Remote IP: {RemoteIp}", context.Connection.RemoteIpAddress);
    logger.LogInformation("Forwarded For: {ForwardedFor}", context.Request.Headers["X-Forwarded-For"]);
    logger.LogInformation("Forwarded: {Forwarded}", context.Request.Headers["Forwarded"]);

    await next();

    logger.LogInformation("=== RESPONSE: {StatusCode} ===", context.Response.StatusCode);
});
```

### Health Check Endpoint

```csharp
// Add to Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

app.MapHealthChecks("/health");
```

## Error Code Reference

| Error | HTTP Status | Source | Resolution |
|-------|-------------|--------|------------|
| Missing required header | 400 | `GetHeaderValue` | Client must send header |
| Invalid HMAC | 401 | Derived middleware | Check secret, timestamp, canonical request |
| Client inactive | 403 | Derived middleware | Activate client in registry |
| Rate limited | 429 | Derived middleware | Implement backoff/retry |
| Body too large | 413 | ASP.NET Core | Increase limit or reject early |
| Request timeout | 408/504 | Infrastructure | Check upstream timeouts |
| Internal error | 500 | Unhandled exception | Check logs, fix bug |

## Performance Issues

### High Latency

**Checklist:**
- [ ] `GetClientHostname` called in hot path? → Cache or remove
- [ ] Synchronous I/O in middleware? → Make async
- [ ] Blocking `.Result` or `.Wait()`? → Use `await`
- [ ] Excessive logging? → Reduce level, use structured logging
- [ ] DNS resolution on every request? → Cache hostnames

### High CPU

**Checklist:**
- [ ] Cryptographic operations per request? → Cache keys, use hardware acceleration
- [ ] Regex in hot path? → Compile regex, cache
- [ ] LINQ allocations? → Use loops, `List<T>`, `Span<T>`
- [ ] JSON serialization in middleware? → Move to controllers

## Getting Help

1. **Check logs** — Enable `Trace` level for middleware namespace
2. **Reproduce locally** — Use `dotnet run` with same config
3. **Search issues** — https://github.com/hmlendea/nuciapi.middleware/issues
4. **Create issue** — Include:
   - .NET version (`dotnet --version`)
   - Package version
   - Minimal reproduction
   - Relevant logs (sanitized)
   - Expected vs actual behavior

## Related Documents

- [Error Handling](./error-handling.md) — Error response patterns
- [Logging](./logging.md) — Diagnostic logging
- [Security](./security.md) — HMAC troubleshooting
- [Client IP Extraction Flow](flows/client-ip-extraction.md) — IP issues
- [Configuration](./configuration.md) — Forwarded headers setup