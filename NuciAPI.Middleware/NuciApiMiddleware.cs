using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using NuciWeb.HTTP;

namespace NuciAPI.Middleware
{
    public abstract class NuciApiMiddleware(
        RequestDelegate next)
    {
        private static readonly string[] ClientIpForwardingHeaderNames =
        [
            "X-Forwarded-For",
            "Forwarded",
            "X-Real-IP",
            "CF-Connecting-IP",
            "True-Client-IP"
        ];

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

            return UrlDecode(rawValue);
        }

        protected List<string> GetClientHostname(HttpContext context)
            => NetworkUtils.GetHostnames(GetClientIpAddress(context));

        protected string GetClientIpAddress(HttpContext context)
        {
            foreach (string headerName in ClientIpForwardingHeaderNames)
            {
                string headerValue = context.Request.Headers[headerName].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(headerValue))
                {
                    continue;
                }

                if (TryGetClientIpFromHeaderValue(headerName, headerValue, out string clientIpAddress))
                {
                    return clientIpAddress;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        protected static string UrlDecode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.Contains('%'))
            {
                return text;
            }

            try
            {
                return Uri.UnescapeDataString(text);
            }
            catch (UriFormatException)
            {
                return text;
            }
        }

        private static bool TryGetClientIpFromHeaderValue(string headerName, string headerValue, out string clientIpAddress)
        {
            clientIpAddress = null;

            IEnumerable<string> ipCandidates = headerName == "Forwarded"
                ? GetForwardedHeaderIpCandidates(headerValue)
                : headerValue.Split(',').Select(x => x.Trim());

            foreach (string ipCandidate in ipCandidates)
            {
                if (!TryNormalizeIpAddress(ipCandidate, out string normalizedIpAddress))
                {
                    continue;
                }

                clientIpAddress = normalizedIpAddress;
                return true;
            }

            return false;
        }

        private static IEnumerable<string> GetForwardedHeaderIpCandidates(string forwardedHeaderValue)
        {
            foreach (string forwardedEntry in forwardedHeaderValue.Split(','))
            {
                foreach (string forwardedToken in forwardedEntry.Split(';'))
                {
                    string token = forwardedToken.Trim();

                    if (!token.StartsWith("for=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    yield return token.Substring(4).Trim();
                }
            }
        }

        private static bool TryNormalizeIpAddress(string rawValue, out string normalizedIpAddress)
        {
            normalizedIpAddress = null;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            string value = rawValue.Trim().Trim('"');

            if (value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                int endBracketIndex = value.IndexOf(']');

                if (endBracketIndex > 1)
                {
                    value = value.Substring(1, endBracketIndex - 1);
                }
            }
            else
            {
                int separatorIndex = value.LastIndexOf(':');

                if (separatorIndex > 0 && value.Contains('.') && value.IndexOf(':') == separatorIndex)
                {
                    value = value.Substring(0, separatorIndex);
                }
            }

            if (!IPAddress.TryParse(value, out IPAddress parsedIpAddress))
            {
                return false;
            }

            normalizedIpAddress = parsedIpAddress.ToString();
            return true;
        }
    }
}