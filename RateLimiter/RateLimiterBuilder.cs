using Microsoft.Extensions.DependencyInjection;

namespace RateLimiter
{
    public class RateLimiterBuilder
    {
        internal RateLimiterOptions Options { get; }
        internal IServiceCollection Services { get; }

        public RateLimiterBuilder(IServiceCollection services, RateLimiterOptions options)
        {
            Services = services;
            Options = options;
        }
    }
}
