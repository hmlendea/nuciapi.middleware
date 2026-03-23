using System;

namespace NuciAPI.Middleware
{
    /// <summary>
    /// Exception thrown when a request with the same client ID, request ID and path has already been processed within the allowed time window.
    /// </summary>
    /// <param name="requestId">The identifier of the request that has already been processed.</param>
    public class RequestAlreadyProcessedException(string requestId)
        : Exception($"Request '{requestId}' has already been processed.")
    {
    }
}
