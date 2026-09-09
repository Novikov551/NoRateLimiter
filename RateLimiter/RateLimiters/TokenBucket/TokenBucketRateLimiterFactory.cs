namespace RateLimiter.RateLimiters.TokenBucket
{
    public class TokenBucketRateLimiterFactory : IAlgorithmRateLimiterFactory
    {
        public TokenBucketRateLimiterFactory()
        {
        }

        public AlgorithmType Algorithm => AlgorithmType.TokenBucket;

        public IRateLimiter Create(RateLimiterPolicy policy)
        {
            if(policy is not TokenBucketRateLimiterPolicy tbp)
            {
                throw new InvalidCastException($"Expected {nameof(TokenBucketRateLimiterPolicy)}, but got {policy.GetType().Name}");
            }

            return new TokenBucket(tbp.TokensPerPeriod, tbp.TokenLimit);
        }

        public int GetDefaultRemaining(RateLimiterPolicy policy)
        {
            if (policy is not TokenBucketRateLimiterPolicy tbp)
            {
                throw new InvalidCastException($"Expected {nameof(TokenBucketRateLimiterPolicy)}, but got {policy.GetType().Name}");
            }

            return tbp.TokenLimit;
        }
    }
}
