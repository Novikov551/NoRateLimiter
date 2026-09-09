namespace RateLimiter.RateLimiters
{
    public interface IAlgorithmRateLimiterFactory
    {
        AlgorithmType Algorithm { get; }

        IRateLimiter Create(RateLimiterPolicy policy);

        int GetDefaultRemaining(RateLimiterPolicy rateLimiterPolicy);
    }
}
