using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace NuciAPI.Middleware
{
    public sealed class ReplayProtectionMiddleware(
        RequestDelegate next,
        IMemoryCache memoryCache)
        : NuciApiMiddleware(next)
    {
        private readonly IMemoryCache memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

        public override async Task InvokeAsync(HttpContext context)
        {
            string clientId = GetHeaderValue(context, NuciApiHeaderNames.ClientId);
            string requestId = GetHeaderValue(context, NuciApiHeaderNames.RequestId);
            DateTimeOffset timestamp = DateTimeOffset.Parse(GetHeaderValue(context, NuciApiHeaderNames.Timestamp));

            TimeSpan allowedSkew = TimeSpan.FromMinutes(5);
            TimeSpan difference = DateTimeOffset.UtcNow - timestamp;

            if (difference > allowedSkew || difference < -allowedSkew)
            {
                throw new BadHttpRequestException(
                    $"This request has expired and is not acceptable anymore.");
            }

            string cacheKey = $"nonce:{clientId}:{requestId}:{context.Request.Path}";

            bool alreadyExists = true;

            memoryCache.GetOrCreate(cacheKey, entry =>
            {
                alreadyExists = false;
                entry.AbsoluteExpirationRelativeToNow = allowedSkew;

                return true;
            });

            if (alreadyExists)
            {
                throw new RequestAlreadyProcessedException(requestId);
            }

            await Next(context);
        }
    }
}