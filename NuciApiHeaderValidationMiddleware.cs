using System;
using System.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NuciAPI.Middleware
{
    public sealed class NuciApiHeaderValidationMiddleware(
        RequestDelegate next)
        : NuciApiMiddleware(next)
    {
        public override async Task InvokeAsync(HttpContext context)
        {
            EnforceHeaderExistence(context, NuciApiHeaderNames.ClientId);
            EnforceHeaderExistence(context, NuciApiHeaderNames.RequestId);
            EnforceHeaderExistence(context, NuciApiHeaderNames.Timestamp);

            string timestampRaw = GetHeaderValue(context, NuciApiHeaderNames.Timestamp);

            if (!DateTimeOffset.TryParse(timestampRaw, out DateTimeOffset timestamp))
            {
                throw new SecurityException(
                    $"The '{NuciApiHeaderNames.Timestamp}' header contains an invalid timestamp format.");
            }

            string requestIdRaw = GetHeaderValue(context, NuciApiHeaderNames.RequestId);

            if (!Guid.TryParse(requestIdRaw, out Guid _))
            {
                throw new SecurityException(
                    $"The '{NuciApiHeaderNames.RequestId}' header contains an invalid identifier format.");
            }

            await Next(context);
        }

        private void EnforceHeaderExistence(HttpContext context, string headerName)
        {
            string headerValue = GetHeaderValue(context, headerName);

            if (string.IsNullOrEmpty(headerValue))
            {
                throw new SecurityException($"The '{headerName}' header is missing.");
            }
        }
    }
}