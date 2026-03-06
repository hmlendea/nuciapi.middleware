using System;

namespace NuciAPI.Middleware
{
    /// <summary>
    /// Repository exception.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RequestAlreadyProcessedException"/> exception.
    /// </remarks>
    public class RequestAlreadyProcessedException(string requestId)
        : Exception($"Request '{requestId}' has already been processed.")
    {
    }
}
