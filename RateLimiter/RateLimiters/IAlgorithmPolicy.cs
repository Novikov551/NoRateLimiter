namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Связывает политику с типом алгоритма. Каждый наследник <see cref="RateLimiterPolicy"/>
    /// указывает свой <see cref="Algorithm"/>, по которому <see cref="CompositeRateLimiterFactory"/>
    /// находит нужную фабрику.
    /// </summary>
    public interface IAlgorithmPolicy
    {
        /// <summary>
        /// Тип алгоритма, который использует эта политика.
        /// </summary>
        AlgorithmType Algorithm { get; }
    }
}
