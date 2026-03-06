using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace NuciAPI.Middleware
{
    public sealed class NuciApiReplayProtectionMiddleware(
        RequestDelegate next,
        IMemoryCache memoryCache)
        : NuciApiMiddleware(next)
    {
        private readonly IMemoryCache memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

        public override async Task InvokeAsync(HttpContext context)
        {
            string clientId = GetHeaderValue(context, NuciApiHeaderNames.ClientId);
            string requestId = GetHeaderValue(context, NuciApiHeaderNames.RequestId);
            string timestampRaw = GetHeaderValue(context, NuciApiHeaderNames.Timestamp);

            if (!DateTimeOffset.TryParse(timestampRaw, out DateTimeOffset timestamp))
            {
                throw new UnauthorizedAccessException("Invalid request timestamp.");
            }

            TimeSpan allowedSkew = TimeSpan.FromMinutes(5);
            TimeSpan difference = DateTimeOffset.UtcNow - timestamp;

            if (difference > allowedSkew || difference < -allowedSkew)
            {
                throw new UnauthorizedAccessException("Invalid request timestamp.");
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
                throw new UnauthorizedAccessException("This request has already been processed.");
            }

            await Next(context);
        }
    }
}