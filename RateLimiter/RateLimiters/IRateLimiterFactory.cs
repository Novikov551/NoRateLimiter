namespace RateLimiter.RateLimiters
{
    public interface IRateLimiterFactory
    {
        IRateLimiter Create(string policyName);

        int GetDefaultRemaining(string policyName);
    }
}
