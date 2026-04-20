using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using NuciAPI.Middleware.Security;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests.Security
{
    [TestFixture]
    public sealed class ReplayProtectionMiddlewareTests
    {
        [Test]
        public async Task Given_FirstValidRequest_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            bool wasInvoked = false;
            ReplayProtectionMiddleware middleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            }, memoryCache);

            await middleware.InvokeAsync(CreateValidContext());

            Assert.That(wasInvoked, Is.True);
        }

        [Test]
        public async Task Given_TimestampWithOffsetAndFractionalSecondsAcrossHeaderValidationAndReplay_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            bool wasInvoked = false;
            ReplayProtectionMiddleware replayProtectionMiddleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            }, memoryCache);
            HeaderValidationMiddleware headerValidationMiddleware = new(replayProtectionMiddleware.InvokeAsync);
            DefaultHttpContext context = CreateValidContext(
                timestampHeaderValue: DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("O"));

            await headerValidationMiddleware.InvokeAsync(context);

            Assert.That(wasInvoked, Is.True);
        }

        [Test]
        public async Task Given_UtcTimestampWithFractionalSecondsAcrossHeaderValidationAndReplay_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            bool wasInvoked = false;
            ReplayProtectionMiddleware replayProtectionMiddleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            }, memoryCache);
            HeaderValidationMiddleware headerValidationMiddleware = new(replayProtectionMiddleware.InvokeAsync);
            DefaultHttpContext context = CreateValidContext(
                timestampHeaderValue: DateTime.UtcNow.ToString("O"));

            await headerValidationMiddleware.InvokeAsync(context);

            Assert.That(wasInvoked, Is.True);
        }

        [Test]
        public async Task Given_TimestampWithOffsetAndFractionalSecondsWithinAllowedSkew_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            bool wasInvoked = false;
            ReplayProtectionMiddleware middleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            }, memoryCache);
            DefaultHttpContext context = CreateValidContext(
                timestampHeaderValue: DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("O"));

            await middleware.InvokeAsync(context);

            Assert.That(wasInvoked, Is.True);
        }

        [Test]
        public async Task Given_UtcTimestampWithFractionalSecondsWithinAllowedSkew_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            bool wasInvoked = false;
            ReplayProtectionMiddleware middleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            }, memoryCache);
            DefaultHttpContext context = CreateValidContext(
                timestampHeaderValue: DateTime.UtcNow.ToString("O"));

            await middleware.InvokeAsync(context);

            Assert.That(wasInvoked, Is.True);
        }

        [Test]
        public async Task Given_DuplicateRequest_When_InvokeAsync_Then_ThrowsRequestAlreadyProcessedException()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ReplayProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext firstContext = CreateValidContext();
            DefaultHttpContext secondContext = CreateValidContext(firstContext.Request.Headers[NuciApiHeaderNames.RequestId]!);

            await middleware.InvokeAsync(firstContext);

        Assert.ThrowsAsync<RequestAlreadyProcessedException>(async () => await middleware.InvokeAsync(secondContext));
        }

        [Test]
        public void Given_ExpiredRequest_When_InvokeAsync_Then_ThrowsBadHttpRequestException()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ReplayProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext context = CreateValidContext(timestamp: DateTimeOffset.UtcNow.AddMinutes(-6));

            Assert.ThrowsAsync<BadHttpRequestException>(async () => await middleware.InvokeAsync(context));
        }

        [Test]
        public void Given_RequestTooFarInFuture_When_InvokeAsync_Then_ThrowsBadHttpRequestException()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ReplayProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext context = CreateValidContext(timestamp: DateTimeOffset.UtcNow.AddMinutes(6));

            Assert.ThrowsAsync<BadHttpRequestException>(async () => await middleware.InvokeAsync(context));
        }

        [Test]
        public void Given_NullMemoryCache_When_ConstructingMiddleware_Then_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new ReplayProtectionMiddleware(_ => Task.CompletedTask, null!),
                Throws.ArgumentNullException);
        }

        private static DefaultHttpContext CreateValidContext(
            string? requestId = null,
            DateTimeOffset? timestamp = null,
            string? timestampHeaderValue = null)
        {
            DefaultHttpContext context = new();
            context.Request.Path = "/resource";
            context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-123";
            context.Request.Headers[NuciApiHeaderNames.RequestId] = requestId ?? Guid.NewGuid().ToString().ToUpperInvariant();
            context.Request.Headers[NuciApiHeaderNames.Timestamp] = timestampHeaderValue ?? (timestamp ?? DateTimeOffset.UtcNow).ToString("O");
            return context;
        }
    }
}