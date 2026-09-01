using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace RateLimiter.KeyProviders
{
    public class RateLimiterUserKeyProvider : IRateLimiterKeyProvider
    {
        public string GetKey(HttpContext httpContext)
        {
            return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        }
    }
}