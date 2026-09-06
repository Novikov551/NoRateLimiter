using Microsoft.Extensions.DependencyInjection;

namespace RateLimiter
{
    public class RateLimiterBuilder
    {
        internal RateLimiterOptions Options { get; }
        internal IServiceCollection Services { get; }

        internal RateLimiterBuilder(IServiceCollection services, RateLimiterOptions options)
        {
            Services = services;
            Options = options;
        }
    }
}
