using RateLimiter.RateLimiters;

namespace RateLimiter
{
    public class RateLimiterOptions
    {
        internal Dictionary<string, RateLimiterPolicy> Policies { get; set; } = new Dictionary<string, RateLimiterPolicy>();
    }
}
