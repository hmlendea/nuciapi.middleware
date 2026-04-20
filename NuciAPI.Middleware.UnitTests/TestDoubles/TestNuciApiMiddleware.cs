using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NuciAPI.Middleware.UnitTests.TestDoubles
{
    internal sealed class TestNuciApiMiddleware(RequestDelegate next) : NuciApiMiddleware(next)
    {
        public override Task InvokeAsync(HttpContext context) => Next(context);

        public string GetHeader(HttpContext context, string headerName) => GetHeaderValue(context, headerName);

        public string TryGetHeader(HttpContext context, string headerName) => TryGetHeaderValue(context, headerName);

        public string GetClientIp(HttpContext context) => GetClientIpAddress(context);

        public string Decode(string text) => UrlDecode(text);
    }
}