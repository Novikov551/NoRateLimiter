using Microsoft.AspNetCore.Builder;
using RateLimiter.Middlewares;

namespace RateLimiter.Extensions
{
    public static class IApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app,
            Action<RateLimiterMiddlewareOptions>? configure = null)
        {
            var options = new RateLimiterMiddlewareOptions();

            if(configure != null)
            {
                configure(options);
            }

            return app.UseMiddleware<RateLimiterMiddleware>(options);
        }
    }
}
