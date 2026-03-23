using NuciLog.Core;

namespace NuciAPI.Middleware.Logging
{
    public sealed class MyOperation : Operation
    {
        MyOperation(string name) : base(name) { }

        public static Operation HttpRequest => new MyOperation(nameof(HttpRequest));
    }
}
