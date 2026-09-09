namespace RateLimiter.RateLimiters.TokenBucket
{
    /// <summary>
    /// Фабрика для алгоритма Token Bucket. Создаёт <see cref="TokenBucket"/>
    /// из <see cref="TokenBucketRateLimiterPolicy"/>.
    /// </summary>
    public class TokenBucketRateLimiterFactory : IAlgorithmRateLimiterFactory
    {

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
