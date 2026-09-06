using Microsoft.AspNetCore.Builder;

namespace RateLimiter.Extensions
{
    public static class IApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RateLimiterMiddleware>();
        }
    }
}
