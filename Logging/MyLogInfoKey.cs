using NuciLog.Core;

namespace NuciAPI.Middleware.Logging
{
    internal sealed class MyLogInfoKey : LogInfoKey
    {
        MyLogInfoKey(string name) : base(name) { }

        public static LogInfoKey ClientId => new MyLogInfoKey(nameof(ClientId));
        public static LogInfoKey ElapsedMilliseconds => new MyLogInfoKey(nameof(ElapsedMilliseconds));
        public static LogInfoKey HmacToken => new MyLogInfoKey(nameof(HmacToken));
        public static LogInfoKey IpAddress => new MyLogInfoKey(nameof(IpAddress));
        public static LogInfoKey Method => new MyLogInfoKey(nameof(Method));
        public static LogInfoKey Path => new MyLogInfoKey(nameof(Path));
        public static LogInfoKey QueryString => new MyLogInfoKey(nameof(QueryString));
        public static LogInfoKey RequestId => new MyLogInfoKey(nameof(RequestId));
        public static LogInfoKey StatusCode => new MyLogInfoKey(nameof(StatusCode));
        public static LogInfoKey Timestamp => new MyLogInfoKey(nameof(Timestamp));
    }
}
