namespace NuciAPI.Middleware
{
    internal static class NuciApiHeaderNames
    {
        internal static string ClientId => "X-Client-ID";
        internal static string HmacToken => "X-HMAC";
        internal static string RequestId => "X-Request-ID";
        internal static string Timestamp => "X-Timestamp";
    }
}
