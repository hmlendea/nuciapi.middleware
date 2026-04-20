using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using NuciAPI.Middleware.Security;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests.Security
{
    [TestFixture]
    public sealed class ScannerProtectionMiddlewareTests
    {
        [Test]
        public async Task Given_ForbiddenPath_When_InvokeAsync_Then_BansClientAndBlocksSubsequentRequest()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ScannerProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);

            DefaultHttpContext firstContext = CreateContext("198.51.100.10", "/.env");
            DefaultHttpContext secondContext = CreateContext("198.51.100.10", "/healthy");

            await middleware.InvokeAsync(firstContext);
            await middleware.InvokeAsync(secondContext);

            Assert.Multiple(() =>
            {
                Assert.That(firstContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
                Assert.That(secondContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            });
        }

        [Test]
        public async Task Given_ForbiddenFromHeader_When_InvokeAsync_Then_BlocksRequest()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ScannerProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext context = CreateContext("198.51.100.11", "/health");
            context.Request.Headers["From"] = "oai-searchbot(at)openai.com";

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task Given_RootRequestWithUnsupportedVerb_When_InvokeAsync_Then_BlocksRequest()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ScannerProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext context = CreateContext("198.51.100.12", "/", method: HttpMethods.Head);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task Given_RootRequestWithoutBodyOrQuery_When_InvokeAsync_Then_BlocksRequest()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ScannerProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext context = CreateContext("198.51.100.13", "/", method: HttpMethods.Get);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task Given_RootPostRequestWithBody_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            bool wasInvoked = false;
            ScannerProtectionMiddleware middleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            }, memoryCache);
            DefaultHttpContext context = CreateContext(
                "198.51.100.14",
                "/",
                method: HttpMethods.Post,
                body: "{\"ok\":true}");

            await middleware.InvokeAsync(context);

            Assert.Multiple(() =>
            {
                Assert.That(wasInvoked, Is.True);
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            });
        }

        [Test]
        public async Task Given_ForbiddenQueryPattern_When_InvokeAsync_Then_BlocksRequest()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            ScannerProtectionMiddleware middleware = new(_ => Task.CompletedTask, memoryCache);
            DefaultHttpContext context = CreateContext(
                "198.51.100.15",
                "/search",
                queryString: new QueryString("?XDEBUG_SESSION_START=phpstorm"));

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public void Given_NullMemoryCache_When_ConstructingMiddleware_Then_ThrowsArgumentNullException()
            => Assert.That(
                () => new ScannerProtectionMiddleware(_ => Task.CompletedTask, null!),
                Throws.ArgumentNullException);

        private static DefaultHttpContext CreateContext(
            string ipAddress,
            string path,
            string method = "GET",
            QueryString queryString = default,
            string? body = null)
        {
            DefaultHttpContext context = new();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.QueryString = queryString;
            context.Request.Headers["X-Forwarded-For"] = ipAddress;

            if (body is not null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;
            }

            return context;
        }
    }
}