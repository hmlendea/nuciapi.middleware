using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using NuciAPI.Middleware.Logging;
using NuciLog.Core;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests.Logging
{
    [TestFixture]
    public sealed class RequestLoggingMiddlewareTests
    {
        [Test]
        public async Task Given_SuccessfulRequest_When_InvokeAsync_Then_LogsStartedAndSuccess()
        {
            Mock<ILogger> logger = new();
            RequestLoggingMiddleware middleware = new(_ => Task.CompletedTask, logger.Object);
            DefaultHttpContext context = CreateContext();

            await middleware.InvokeAsync(context);

            Assert.Multiple(() =>
            {
                Assert.That(logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Info)), Is.EqualTo(2));
                Assert.That(logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Error)), Is.EqualTo(0));
            });
        }

        [Test]
        public void Given_DownstreamException_When_InvokeAsync_Then_LogsFailureAndRethrows()
        {
            Mock<ILogger> logger = new();
            RequestLoggingMiddleware middleware = new(_ => throw new InvalidOperationException("boom"), logger.Object);
            DefaultHttpContext context = CreateContext();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await middleware.InvokeAsync(context));

            Assert.Multiple(() =>
            {
                Assert.That(logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Info)), Is.EqualTo(1));
                Assert.That(logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Error)), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Given_NonSuccessfulStatusCode_When_InvokeAsync_Then_LogsFailure()
        {
            Mock<ILogger> logger = new();
            RequestLoggingMiddleware middleware = new(context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            }, logger.Object);
            DefaultHttpContext context = CreateContext();

            await middleware.InvokeAsync(context);

            Assert.Multiple(() =>
            {
                Assert.That(logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Info)), Is.EqualTo(1));
                Assert.That(logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Error)), Is.EqualTo(1));
            });
        }

        private static DefaultHttpContext CreateContext()
        {
            DefaultHttpContext context = new();
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/orders";
            context.Request.QueryString = new QueryString("?page=1");
            context.Request.Headers[NuciApiHeaderNames.ClientId] = "client-123";
            context.Request.Headers[NuciApiHeaderNames.RequestId] = Guid.NewGuid().ToString().ToUpperInvariant();
            context.Request.Headers[NuciApiHeaderNames.Timestamp] = DateTimeOffset.UtcNow.ToString("O");
            context.Request.Headers[NuciApiHeaderNames.HmacToken] = "token";
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.50";
            return context;
        }
    }
}