using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace NuciAPI.Middleware
{
    public abstract class NuciApiMiddleware(
        RequestDelegate next)
    {
        protected readonly RequestDelegate Next = next ?? throw new ArgumentNullException(nameof(next));

        public abstract Task InvokeAsync(HttpContext context);

        protected string GetHeaderValue(HttpContext context, string headerName)
        {
            if (!context.Request.Headers.ContainsKey(headerName))
            {
                throw new BadHttpRequestException(
                    $"The '{headerName}' header is missing.");
            }

            return TryGetHeaderValue(context, headerName);
        }

        protected string TryGetHeaderValue(HttpContext context, string headerName)
        {
            string rawValue = context.Request.Headers[headerName].FirstOrDefault();

            if (rawValue is null)
            {
                return null;
            }

            return Uri.UnescapeDataString(rawValue);
        }
    }
}