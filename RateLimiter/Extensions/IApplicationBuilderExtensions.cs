using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace RateLimiter.Extensions
{
    public static class IApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app)
        {
            var builder = app.ApplicationServices.GetRequiredService<RateLimiterBuilder>();
            builder.Build();

            return app.UseMiddleware<RateLimiterMiddleware>();
        }
    }
}
