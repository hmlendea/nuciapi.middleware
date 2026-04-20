using NuciAPI.Middleware.Security;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests.Security
{
    [TestFixture]
    public sealed class RequestAlreadyProcessedExceptionTests
    {
        [Test]
        public void Given_RequestId_When_ConstructingException_Then_MessageContainsRequestId()
        {
            RequestAlreadyProcessedException exception = new("REQ-123");

            Assert.That(exception.Message, Is.EqualTo("Request 'REQ-123' has already been processed."));
        }
    }
}