using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NuciAPI.Middleware.Security
{
    internal sealed class HeaderValidationMiddleware(
        RequestDelegate next)
        : NuciApiMiddleware(next)
    {
        public override async Task InvokeAsync(HttpContext context)
        {
            ValidateClientId(context);
            ValidateRequestId(context);
            ValidateTimestamp(context);

            await Next(context);
        }

        private void ValidateClientId(HttpContext context)
        {
            string clientId = GetHeaderValue(context, NuciApiHeaderNames.ClientId);

            if (clientId.Length <= 3)
            {
                throw new BadHttpRequestException(
                    $"The '{NuciApiHeaderNames.ClientId}' header contains an invalid client identifier.");
            }
        }

        private void ValidateRequestId(HttpContext context)
        {
            string requestIdRaw = GetHeaderValue(context, NuciApiHeaderNames.RequestId);

            if (!Guid.TryParse(requestIdRaw, out Guid _) ||
                !requestIdRaw.Equals(requestIdRaw.ToUpper()))
            {
                throw new BadHttpRequestException(
                    $"The '{NuciApiHeaderNames.RequestId}' header contains an invalid identifier format.");
            }
        }

        private void ValidateTimestamp(HttpContext context)
        {
            string timestampRaw = GetHeaderValue(context, NuciApiHeaderNames.Timestamp);

            if (!DateTimeOffset.TryParse(timestampRaw, out DateTimeOffset _))
            {
                throw new BadHttpRequestException(
                    $"The '{NuciApiHeaderNames.Timestamp}' header contains an invalid timestamp format.");
            }
        }
    }
}