using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace NuciAPI.Middleware
{
    public static class NuciApiSecurityExtensions
    {
        public static IServiceCollection AddNuciApiReplayProtection(
            this IServiceCollection services)
        {
            services.AddMemoryCache();

            return services;
        }

        public static IApplicationBuilder UseNuciApiExceptionHandling(
            this IApplicationBuilder app)
            => app.UseMiddleware<ExceptionHandlingMiddleware>();

        public static IApplicationBuilder UseNuciApiHeaderValidation(
            this IApplicationBuilder app)
            => app.UseMiddleware<HeaderValidationMiddleware>();

        public static IApplicationBuilder UseNuciApiReplayProtection(
            this IApplicationBuilder app)
            => app.UseMiddleware<ReplayProtectionMiddleware>();
    }
}