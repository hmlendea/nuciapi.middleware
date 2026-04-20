using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware.Security;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests.Security
{
    [TestFixture]
    public sealed class HeaderValidationMiddlewareTests
    {
        [Test]
        public async Task Given_ValidHeaders_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            bool wasInvoked = false;
            HeaderValidationMiddleware middleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(CreateValidContext());

            Assert.That(wasInvoked, Is.True);
        }

        [Test]
        public void Given_ShortClientId_When_InvokeAsync_Then_ThrowsBadHttpRequestException()
        {
            HeaderValidationMiddleware middleware = new(_ => Task.CompletedTask);
            DefaultHttpContext context = CreateValidContext();
            context.Request.Headers[NuciApiHeaderNames.ClientId] = "abc";

            Assert.ThrowsAsync<BadHttpRequestException>(async () => await middleware.InvokeAsync(context));
        }

        [Test]
        public void Given_LowercaseRequestId_When_InvokeAsync_Then_ThrowsBadHttpRequestException()
        {
            HeaderValidationMiddleware middleware = new(_ => Task.CompletedTask);
            DefaultHttpContext context = CreateValidContext();
            context.Request.Headers[NuciApiHeaderNames.RequestId] = Guid.NewGuid().ToString();

            Assert.ThrowsAsync<BadHttpRequestException>(async () => await middleware.InvokeAsync(context));
        }

        [Test]
        public void Given_InvalidTimestamp_When_InvokeAsync_Then_ThrowsBadHttpRequestException()
        {
            HeaderValidationMiddleware middleware = new(_ => Task.CompletedTask);
            DefaultHttpContext context = CreateValidContext();
            context.Request.Headers[NuciApiHeaderNames.Timestamp] = "not-a-timestamp";

            Assert.ThrowsAsync<BadHttpRequestException>(async () => await middleware.InvokeAsync(context));
        }

        private static DefaultHttpContext CreateValidContext()
        {
            DefaultHttpContext context = new();
            context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-123";
            context.Request.Headers[NuciApiHeaderNames.RequestId] = Guid.NewGuid().ToString().ToUpperInvariant();
            context.Request.Headers[NuciApiHeaderNames.Timestamp] = DateTimeOffset.UtcNow.ToString("O");

            return context;
        }
    }
}