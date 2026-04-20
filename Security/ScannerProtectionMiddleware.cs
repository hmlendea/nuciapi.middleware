using System;
using System.IO;
using System.Text;
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

        private static readonly string[] SafeVerbs = ["POST", "GET", "PUT", "DELETE", "PATCH"];

        private static readonly string[] ForbiddenFromHeaders =
        [
            "oai-searchbot(at)openai.com",
        ];

        private static readonly Regex[] ForbiddenResourcePatterns =
        [
            CreateExactPathRegex("/_environment"),
            CreateExactPathRegex("/_ignition/execute-solution"),
            CreateExactPathRegex("/.aws/config"),
            CreateExactPathRegex("/.aws/credentials"),
            CreateExactPathRegex("/.DS_Store"),
            CreateExactPathRegex("/.env.production"),
            CreateExactPathRegex("/.env"),
            CreateExactPathRegex("/.netrc"),
            CreateExactPathRegex("/.npmrc"),
            CreateExactPathRegex("/.pgpass"),
            CreateExactPathRegex("/.streamlit/secrets.toml"),
            CreateExactPathRegex("/.vscode/sftp.json"),
            CreateExactPathRegex("/.well-known/security.txt"),
            CreateExactPathRegex("/@vite/env"),
            CreateExactPathRegex("/actuator/env"),
            CreateExactPathRegex("/actuator/health"),
            CreateExactPathRegex("/actuator/heapdump"),
            CreateExactPathRegex("/api/gql"),
            CreateExactPathRegex("/api/graphql"),
            CreateExactPathRegex("/app/"),
            CreateExactPathRegex("/application.properties"),
            CreateExactPathRegex("/application.yml"),
            CreateExactPathRegex("/appsettings.json"),
            CreateExactPathRegex("/assets/js/auth.js"),
            CreateExactPathRegex("/assets/js/message.js"),
            CreateExactPathRegex("/assets/js/qr_modal.js"),
            CreateExactPathRegex("/autodiscover/autodiscover.json"),
            CreateExactPathRegex("/aws.env"),
            CreateExactPathRegex("/bot-connect.js"),
            CreateExactPathRegex("/config.json"),
            CreateExactPathRegex("/config.php.bak"),
            CreateExactPathRegex("/css/support_parent.css"),
            CreateExactPathRegex("/currentsetting.htm"),
            CreateExactPathRegex("/debug/default/view"),
            CreateExactPathRegex("/developmentserver/metadatauploader"),
            CreateExactPathRegex("/ecp/Current/exporttool/microsoft.exchange.ediscovery.exporttool.application"),
            CreateExactPathRegex("/error_log"),
            CreateExactPathRegex("/geoserver/web/"),
            CreateExactPathRegex("/go"),
            CreateExactPathRegex("/graphql"),
            CreateExactPathRegex("/graphql/api"),
            CreateExactPathRegex("/info.php"),
            CreateExactPathRegex("/js/lkk_ch.js"),
            CreateExactPathRegex("/js/twint_ch.js"),
            CreateExactPathRegex("/lander/"),
            CreateExactPathRegex("/lander/pkn-orlen-lend/"),
            CreateExactPathRegex("/lander/pl_4/index.html"),
            CreateExactPathRegex("/lander/pl---immediate-bitwave-2/"),
            CreateExactPathRegex("/lander/pl---interpol0/"),
            CreateExactPathRegex("/lander/pl-orlen/"),
            CreateExactPathRegex("/lander/polskagoldai/index.html"),
            CreateExactPathRegex("/login.action"),
            CreateExactPathRegex("/mcp"),
            CreateExactPathRegex("/next"),
            CreateExactPathRegex("/owa/auth/logon.aspx"),
            CreateExactPathRegex("/owa/auth/x.js"),
            CreateExactPathRegex("/profiler/_phpinfo"),
            CreateExactPathRegex("/r"),
            CreateExactPathRegex("/redirect-to"),
            CreateExactPathRegex("/redirect/"),
            CreateExactPathRegex("/redirect/testdomain.com"),
            CreateExactPathRegex("/remote/login"),
            CreateExactPathRegex("/robots.txt"),
            CreateExactPathRegex("/SDK/webLanguage"),
            CreateExactPathRegex("/security.txt"),
            CreateExactPathRegex("/server-info.php"),
            CreateExactPathRegex("/server-status.php"),
            CreateExactPathRegex("/sitemap.xml"),
            CreateExactPathRegex("/sse"),
            CreateExactPathRegex("/telescope/requests"),
            CreateExactPathRegex("/terraform.tfstate"),
            CreateExactPathRegex("/terraform.tfvars"),
            CreateExactPathRegex("/trace.axd"),
            CreateExactPathRegex("/url"),
            CreateExactPathRegex("/v2/_catalog"),
            CreateExactPathRegex("/web.config"),
            CreateExactPathRegex("/webroot/index.php/_environment"),
            CreateRawRegex("^.*/pom.properties$"),
            CreateRawRegex("^.*/secrets\\.(json|yml)$"),
            CreateRawRegex("^.*\\.php\\.(backuo|bak|old|swap)$"),
            CreateRawRegex("^.*\\.php~$"),
            CreateRawRegex("^.*\\.php$"),
            CreateRawRegex("^.*\\.sql$"),
            CreateRawRegex("^.*\\debug.log$"),
            CreateRawRegex("^/.*/\\.env$"),
            CreateRawRegex("^/.*/prodtest$"),
            CreateRawRegex("^/\\.env[~]?$"),
            CreateRawRegex("^/\\.env\\.[a-z]+$"),
            CreateRawRegex("^/\\.git.*$"),
            CreateRawRegex("^/console(?:/.*)?$"),
            CreateRawRegex("^/lander/.*$"),
            CreateRawRegex("^/static/style/[^/]*/index.js$"),
        ];

        private static readonly Regex[] ForbiddenQueryPatterns =
        [
            CreateRawRegex("(?:^|&)XDEBUG_SESSION_START=phpstorm(?:&|$)"),
            CreateRawRegex("(?:^|&)rest_route=/wp/v2/users/?(?:&|$)"),
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

            if (await ShouldBanRequestAsync(context.Request))
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

        private async Task<bool> ShouldBanRequestAsync(HttpRequest request)
        {
            string path = UrlDecode(request.Path.ToString());

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fromHeader = TryGetHeaderValue(request, "From");

            if (!string.IsNullOrWhiteSpace(fromHeader) &&
                ForbiddenFromHeaders.Contains(fromHeader, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (Regex forbiddenResourcePattern in ForbiddenResourcePatterns)
            {
                if (forbiddenResourcePattern.IsMatch(path))
                {
                    return true;
                }
            }

            string queryString = request.QueryString.ToString().TrimStart('?');

            if (!string.IsNullOrWhiteSpace(queryString))
            {
                foreach (Regex forbiddenQueryPattern in ForbiddenQueryPatterns)
                {
                    if (forbiddenQueryPattern.IsMatch(queryString))
                    {
                        return true;
                    }
                }
            }

            if (path.Equals("/", StringComparison.OrdinalIgnoreCase))
            {
                if (!SafeVerbs.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                string body = await ReadRequestBodyAsStringAsync(request);

                if (string.IsNullOrWhiteSpace(queryString) &&
                    string.IsNullOrWhiteSpace(body))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<string> ReadRequestBodyAsStringAsync(HttpRequest request)
        {
            if (request.Body is null)
            {
                return null;
            }

            if (request.ContentLength == 0)
            {
                return null;
            }

            request.EnableBuffering();
            request.Body.Position = 0;

            using StreamReader reader = new(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);

            string body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            return body;
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