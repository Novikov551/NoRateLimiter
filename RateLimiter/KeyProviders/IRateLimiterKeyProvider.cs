using Microsoft.AspNetCore.Http;

namespace RateLimiter.KeyProviders
{
    public interface IRateLimiterKeyProvider
    {
        string GetKey(HttpContext httpContext);
    }
}
