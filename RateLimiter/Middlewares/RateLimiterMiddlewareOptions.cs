using RateLimiter.Middlewares.KeyProviders;
using RateLimiter.RateLimiters;

namespace RateLimiter.Middlewares
{
    public class RateLimiterMiddlewareOptions
    {
        public IRateLimiterKeyProvider KeyProvider { get; set; } = new RateLimiterUserKeyProvider();
        public RateLimiterType Type { get; set; } = RateLimiterType.TokenBucket;
        public int? RequestsLimit { get; set; } = null;
        public TimeSpan? Window { get; set; } = null;
        public int? Capacity { get; set; } = 20;
        public int? RefillRate { get; set; } = 2;
    }
}
