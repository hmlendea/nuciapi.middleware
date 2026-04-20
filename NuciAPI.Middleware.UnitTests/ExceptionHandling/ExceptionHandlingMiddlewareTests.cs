using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciAPI.Middleware.ExceptionHandling;
using NuciAPI.Middleware.Security;
using NUnit.Framework;

namespace NuciAPI.Middleware.UnitTests.ExceptionHandling
{
    [TestFixture]
    public sealed class ExceptionHandlingMiddlewareTests
    {
        [TestCase(typeof(BadHttpRequestException), (int)HttpStatusCode.BadRequest)]
        [TestCase(typeof(ArgumentException), (int)HttpStatusCode.BadRequest)]
        [TestCase(typeof(FormatException), (int)HttpStatusCode.BadRequest)]
        [TestCase(typeof(SecurityException), (int)HttpStatusCode.Forbidden)]
        [TestCase(typeof(UnauthorizedAccessException), (int)HttpStatusCode.Forbidden)]
        [TestCase(typeof(HttpRequestException), (int)HttpStatusCode.ServiceUnavailable)]
        [TestCase(typeof(TaskCanceledException), (int)HttpStatusCode.ServiceUnavailable)]
        [TestCase(typeof(TimeoutException), (int)HttpStatusCode.ServiceUnavailable)]
        [TestCase(typeof(AuthenticationException), (int)HttpStatusCode.Unauthorized)]
        [TestCase(typeof(KeyNotFoundException), (int)HttpStatusCode.NotFound)]
        [TestCase(typeof(RequestAlreadyProcessedException), (int)HttpStatusCode.Conflict)]
        [TestCase(typeof(NotImplementedException), (int)HttpStatusCode.NotImplemented)]
        [TestCase(typeof(OperationCanceledException), StatusCodes.Status499ClientClosedRequest)]
        [TestCase(typeof(Exception), (int)HttpStatusCode.InternalServerError)]
        public async Task Given_DownstreamException_When_InvokeAsync_Then_WritesMappedStatusCode(Type exceptionType, int expectedStatusCode)
        {
            ExceptionHandlingMiddleware middleware = new(_ => throw CreateException(exceptionType));
            DefaultHttpContext context = CreateContext();

            await middleware.InvokeAsync(context);

            Assert.Multiple(() =>
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(expectedStatusCode));
                Assert.That(context.Response.ContentType, Does.StartWith("application/json"));
                Assert.That(ReadResponseBody(context), Is.Not.Empty);
            });
        }

        [Test]
        public async Task Given_NoException_When_InvokeAsync_Then_InvokesNextDelegate()
        {
            bool wasInvoked = false;
            ExceptionHandlingMiddleware middleware = new(_ =>
            {
                wasInvoked = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(CreateContext());

            Assert.That(wasInvoked, Is.True);
        }

        private static DefaultHttpContext CreateContext()
        {
            DefaultHttpContext context = new();
            context.Response.Body = new MemoryStream();

            return context;
        }

        private static string ReadResponseBody(DefaultHttpContext context)
        {
            context.Response.Body.Position = 0;
            using StreamReader reader = new(context.Response.Body, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private static Exception CreateException(Type exceptionType)
            => exceptionType == typeof(BadHttpRequestException) ? new BadHttpRequestException("Bad request")
            : exceptionType == typeof(ArgumentException) ? new ArgumentException("Invalid argument")
            : exceptionType == typeof(FormatException) ? new FormatException("Invalid format")
            : exceptionType == typeof(SecurityException) ? new SecurityException("Forbidden")
            : exceptionType == typeof(UnauthorizedAccessException) ? new UnauthorizedAccessException("Forbidden")
            : exceptionType == typeof(HttpRequestException) ? new HttpRequestException("Dependency unavailable")
            : exceptionType == typeof(TaskCanceledException) ? new TaskCanceledException("Cancelled")
            : exceptionType == typeof(TimeoutException) ? new TimeoutException("Timed out")
            : exceptionType == typeof(AuthenticationException) ? new AuthenticationException("Auth failed")
            : exceptionType == typeof(KeyNotFoundException) ? new KeyNotFoundException("Not found")
            : exceptionType == typeof(RequestAlreadyProcessedException) ? new RequestAlreadyProcessedException("REQ-1")
            : exceptionType == typeof(NotImplementedException) ? new NotImplementedException("Not implemented")
            : exceptionType == typeof(OperationCanceledException) ? new OperationCanceledException("Client closed")
            : new Exception("Unhandled");
    }
}