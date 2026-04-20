using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NuciLog.Core;
using NUnit.Framework;
using System.Linq;

namespace NuciAPI.Middleware.UnitTests
{
    [TestFixture]
    public sealed class NuciApiSecurityExtensionsTests
    {
        [Test]
        public void Given_ServiceCollection_When_AddNuciApiScannerProtection_Then_ReturnsSameCollectionAndRegistersMemoryCache()
        {
            ServiceCollection services = [];

            IServiceCollection result = services.AddNuciApiScannerProtection();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.SameAs(services));
                Assert.That(services.Any(x => x.ServiceType == typeof(IMemoryCache)), Is.True);
            });
        }

        [Test]
        public void Given_ServiceCollection_When_AddNuciApiReplayProtection_Then_ReturnsSameCollectionAndRegistersMemoryCache()
        {
            ServiceCollection services = [];

            IServiceCollection result = services.AddNuciApiReplayProtection();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.SameAs(services));
                Assert.That(services.Any(x => x.ServiceType == typeof(IMemoryCache)), Is.True);
            });
        }

        [Test]
        public void Given_ApplicationBuilder_When_UseSecurityExtensions_Then_ReturnsSameBuilder()
        {
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(Mock.Of<ILogger>())
                .BuildServiceProvider();
            ApplicationBuilder app = new(serviceProvider);

            Assert.Multiple(() =>
            {
                Assert.That(app.UseNuciApiExceptionHandling(), Is.SameAs(app));
                Assert.That(app.UseNuciApiRequestLogging(), Is.SameAs(app));
                Assert.That(app.UseNuciApiHeaderValidation(), Is.SameAs(app));
                Assert.That(app.UseNuciApiScannerProtection(), Is.SameAs(app));
                Assert.That(app.UseNuciApiReplayProtection(), Is.SameAs(app));
            });
        }
    }
}