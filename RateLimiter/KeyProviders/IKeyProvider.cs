using Microsoft.AspNetCore.Http;

namespace RateLimiter.KeyProviders
{
    public interface IKeyProvider
    {
        string GetKey(HttpContext httpContext);
    }
}
