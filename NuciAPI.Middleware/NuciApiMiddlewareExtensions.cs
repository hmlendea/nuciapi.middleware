using Microsoft.AspNetCore.Builder;

using NuciAPI.Middleware.ExceptionHandling;
using NuciAPI.Middleware.Logging;

namespace NuciAPI.Middleware
{
    public static class NuciApiSecurityExtensions
    {
        public static IApplicationBuilder UseNuciApiExceptionHandling(
            this IApplicationBuilder app)
            => app.UseMiddleware<ExceptionHandlingMiddleware>();

        public static IApplicationBuilder UseNuciApiRequestLogging(
            this IApplicationBuilder app)
            => app.UseMiddleware<RequestLoggingMiddleware>();
    }
}