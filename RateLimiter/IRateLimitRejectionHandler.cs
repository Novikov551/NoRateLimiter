using Microsoft.AspNetCore.Http;

namespace RateLimiter
{
    public interface IRateLimitRejectionHandler
    {
        Task HandleAsync(HttpContext httpContext);
    }
}
