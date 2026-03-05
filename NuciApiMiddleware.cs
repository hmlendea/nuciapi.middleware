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
            => context.Request.Headers[headerName].FirstOrDefault();
    }
}