namespace RateLimiter.RateLimiters
{
    public interface IAlgorithmPolicy
    {
        AlgorithmType Algorithm { get; }
    }
}
