using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware.UnitTests.TestDoubles;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests
{
    [TestFixture]
    public sealed class NuciApiMiddlewareTests
    {
        [Test]
        public void Given_NullNextDelegate_When_ConstructingMiddleware_Then_ThrowsArgumentNullException()
        {
            Assert.That(() => new TestNuciApiMiddleware(null!), Throws.ArgumentNullException);
        }

        [Test]
        public void Given_MissingHeader_When_GetHeaderValue_Then_ThrowsBadHttpRequestException()
        {
            TestNuciApiMiddleware middleware = CreateMiddleware();
            DefaultHttpContext context = new();

            Assert.That(
                () => middleware.GetHeader(context, NuciApiHeaderNames.ClientId),
                Throws.TypeOf<BadHttpRequestException>());
        }

        [Test]
        public void Given_EncodedHeaderValue_When_TryGetHeaderValue_Then_ReturnsDecodedValue()
        {
            TestNuciApiMiddleware middleware = CreateMiddleware();
            DefaultHttpContext context = new();
            context.Request.Headers[NuciApiHeaderNames.ClientId] = "hello%20world";

            string value = middleware.TryGetHeader(context, NuciApiHeaderNames.ClientId);

            Assert.That(value, Is.EqualTo("hello world"));
        }

        [Test]
        public void Given_XForwardedForHeader_When_GetClientIpAddress_Then_ReturnsFirstValidIp()
        {
            TestNuciApiMiddleware middleware = CreateMiddleware();
            DefaultHttpContext context = new();
            context.Request.Headers["X-Forwarded-For"] = "unknown, 198.51.100.10, 198.51.100.20";

            string value = middleware.GetClientIp(context);

            Assert.That(value, Is.EqualTo("198.51.100.10"));
        }

        [Test]
        public void Given_ForwardedHeaderWithIpv6AndPort_When_GetClientIpAddress_Then_ReturnsNormalizedIp()
        {
            TestNuciApiMiddleware middleware = CreateMiddleware();
            DefaultHttpContext context = new();
            context.Request.Headers["Forwarded"] = "for=unknown;proto=https, for=\"[2001:db8:cafe::17]:4711\";proto=https";

            string value = middleware.GetClientIp(context);

            Assert.That(value, Is.EqualTo("2001:db8:cafe::17"));
        }

        [Test]
        public void Given_NoForwardingHeaders_When_GetClientIpAddress_Then_ReturnsRemoteIpAddress()
        {
            TestNuciApiMiddleware middleware = CreateMiddleware();
            DefaultHttpContext context = new();
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");

            string value = middleware.GetClientIp(context);

            Assert.That(value, Is.EqualTo("203.0.113.5"));
        }

        [Test]
        public void Given_InvalidEncodedValue_When_UrlDecode_Then_ReturnsOriginalText()
        {
            TestNuciApiMiddleware middleware = CreateMiddleware();

            string value = middleware.Decode("%E0%A4%A");

            Assert.That(value, Is.EqualTo("%E0%A4%A"));
        }

        private static TestNuciApiMiddleware CreateMiddleware()
            => new(_ => Task.CompletedTask);
    }
}