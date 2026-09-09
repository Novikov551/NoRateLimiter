namespace RateLimiter.RateLimiters.TokenBucket
{
    public class TokenBucketRateLimiterPolicy : RateLimiterPolicy
    {
        public int TokenLimit { get; set; }
        public int TokensPerPeriod { get; set; }
        public TimeSpan ReplenishmentPeriod { get; set; }

        public override AlgorithmType Algorithm => AlgorithmType.TokenBucket;
    }
}
