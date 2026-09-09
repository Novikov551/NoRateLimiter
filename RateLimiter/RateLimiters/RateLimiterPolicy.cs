namespace RateLimiter.RateLimiters
{
    public abstract class RateLimiterPolicy : IAlgorithmPolicy
    { 
        public string Name { get; internal set; } = string.Empty;

        public abstract AlgorithmType Algorithm { get; }
    }
}
