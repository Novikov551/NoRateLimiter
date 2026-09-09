using RateLimiter.RateLimiters.SlidingWindow;
using RateLimiter.RateLimiters.TokenBucket;

namespace RateLimiter.Extensions
{
    public static class RateLimiterOptionsExtensions
    {
        public static RateLimiterOptions AddTokenBucketLimiter(this RateLimiterOptions options,
            string name,
            Action<TokenBucketRateLimiterPolicy> configure)
        {
            var policy = new TokenBucketRateLimiterPolicy
            {
                Name = name
            };

            configure(policy);

            if (policy.TokenLimit <= 0)
            {
                throw new ArgumentException("TokenLimit must be greater than zero.", nameof(policy));
            }

            if (policy.TokensPerPeriod <= 0)
            {
                throw new ArgumentException("TokensPerPeriod must be greater than zero.", nameof(policy));
            }

            if (policy.ReplenishmentPeriod <= TimeSpan.Zero)
            {
                throw new ArgumentException("ReplenishmentPeriod must be greater than zero.", nameof(policy));
            }

            options.Policies[name] = policy;

            return options;
        }

        public static RateLimiterOptions AddSlidingWindowLimiter(this RateLimiterOptions options,
           string name,
           Action<SlidingWindowRateLimiterPolicy> configure)
        {
            var policy = new SlidingWindowRateLimiterPolicy
            {
                Name = name
            };

            configure(policy);

            if (policy.RequestsLimit <= 0)
            {
                throw new ArgumentException("RequestsLimit must be greater than zero.", nameof(policy));
            }

            if (policy.Window <= TimeSpan.Zero)
            {
                throw new ArgumentException("Window must be greater than zero.", nameof(policy));
            }

            options.Policies[name] = policy;

            return options;
        }
    }
}
