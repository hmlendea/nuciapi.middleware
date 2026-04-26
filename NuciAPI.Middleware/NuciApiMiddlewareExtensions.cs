using Microsoft.AspNetCore.Builder;

using NuciAPI.Middleware.Logging;

namespace NuciAPI.Middleware
{
    public static class NuciApiSecurityExtensions
    {
        public static IApplicationBuilder UseNuciApiRequestLogging(
            this IApplicationBuilder app)
            => app.UseMiddleware<RequestLoggingMiddleware>();
    }
}