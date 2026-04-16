using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace NuciAPI.Middleware.Security
{
    internal sealed class ScannerProtectionMiddleware(
        RequestDelegate next,
        IMemoryCache memoryCache)
        : NuciApiMiddleware(next)
    {
        private static readonly TimeSpan BanDuration = TimeSpan.FromHours(10);

        private static readonly Regex[] ForbiddenResourcePatterns =
        [
            CreateExactPathRegex("/.DS_Store"),
            CreateExactPathRegex("/.vscode/sftp.json"),
            CreateExactPathRegex("/.well-known/security.txt"),
            CreateExactPathRegex("/@vite/env"),
            CreateExactPathRegex("/actuator/env"),
            CreateExactPathRegex("/api/gql"),
            CreateExactPathRegex("/assets/js/auth.js"),
            CreateExactPathRegex("/assets/js/message.js"),
            CreateExactPathRegex("/bot-connect.js"),
            CreateExactPathRegex("/config.json"),
            CreateExactPathRegex("/debug/default/view"),
            CreateExactPathRegex("/ecp/Current/exporttool/microsoft.exchange.ediscovery.exporttool.application"),
            CreateExactPathRegex("/graphql/api"),
            CreateExactPathRegex("/info.php"),
            CreateExactPathRegex("/login.action"),
            CreateExactPathRegex("/robots.txt"),
            CreateExactPathRegex("/security.txt"),
            CreateExactPathRegex("/sitemap.xml"),
            CreateExactPathRegex("/static/style/protect/index.js"),
            CreateExactPathRegex("/static/style/sys_files/index.js"),
            CreateExactPathRegex("/telescope/requests"),
            CreateExactPathRegex("/trace.axd"),
            CreateExactPathRegex("/v2/_catalog"),
            CreateRawRegex("^/\\.git/.*$"),
            CreateRawRegex("^/console(?:/.*)?$"),
        ];

        private readonly IMemoryCache memoryCache = memoryCache ??
            throw new ArgumentNullException(nameof(memoryCache));

        public override async Task InvokeAsync(HttpContext context)
        {
            string clientIpAddress = GetClientIpAddress(context);

            if (IsIpAddressBanned(clientIpAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return;
            }

            if (ShouldBanRequest(context.Request.Path))
            {
                BanIpAddress(clientIpAddress);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return;
            }

            await Next(context);
        }

        private bool IsIpAddressBanned(string clientIpAddress)
            => !string.IsNullOrWhiteSpace(clientIpAddress) &&
               memoryCache.TryGetValue(GetBannedIpAddressCacheKey(clientIpAddress), out bool _);

        private static bool ShouldBanRequest(PathString requestPath)
        {
            string path = requestPath.ToString();

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            foreach (Regex forbiddenResourcePattern in ForbiddenResourcePatterns)
            {
                if (forbiddenResourcePattern.IsMatch(path))
                {
                    return true;
                }
            }

            return false;
        }

        private void BanIpAddress(string clientIpAddress)
        {
            if (string.IsNullOrWhiteSpace(clientIpAddress))
            {
                return;
            }

            memoryCache.Set(
                GetBannedIpAddressCacheKey(clientIpAddress),
                true,
                BanDuration);
        }

        private static string GetBannedIpAddressCacheKey(string clientIpAddress)
            => $"nuciweb.middleware.banned-ip:{clientIpAddress}";

        private static Regex CreateExactPathRegex(string path)
            => CreateRawRegex($"^{Regex.Escape(path)}$");

        private static Regex CreateRawRegex(string pattern)
            => new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    }
}