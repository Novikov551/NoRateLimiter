using Microsoft.AspNetCore.Http;

namespace RateLimiter.Middlewares.KeyProviders
{
    public interface IRateLimiterKeyProvider
    {
        string GetKey(HttpContext httpContext);
    }
}
