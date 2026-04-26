namespace NuciAPI.Middleware
{
    public static class NuciApiHeaderNames
    {
        public static string ClientId => "X-Client-ID";
        public static string HmacToken => "X-HMAC";
        public static string RequestId => "X-Request-ID";
        public static string Timestamp => "X-Timestamp";
    }
}
