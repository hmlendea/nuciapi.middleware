using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests
{
    [TestFixture]
    public sealed class NuciApiHeaderNamesTests
    {
        [Test]
        public void Given_HeaderNames_When_Accessed_Then_ReturnExpectedValues()
            => Assert.Multiple(() =>
            {
                Assert.That(NuciApiHeaderNames.ClientId, Is.EqualTo("X-Client-ID"));
                Assert.That(NuciApiHeaderNames.HmacToken, Is.EqualTo("X-HMAC"));
                Assert.That(NuciApiHeaderNames.RequestId, Is.EqualTo("X-Request-ID"));
                Assert.That(NuciApiHeaderNames.Timestamp, Is.EqualTo("X-Timestamp"));
            });
    }
}