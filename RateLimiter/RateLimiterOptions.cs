using RateLimiter.RateLimiters;

namespace RateLimiter
{
    /// <summary>
    /// Builder-конфигурация для rate limiter. Содержит зарегистрированные
    /// политики. Не зарегистрован в DI — передаётся в <see cref="RateLimiters.CompositeRateLimiterFactory"/>
    /// через замыкание.
    /// </summary>
    public class RateLimiterOptions
    {
        public Dictionary<string, RateLimiterPolicy> Policies { get; } = new Dictionary<string, RateLimiterPolicy>();
    }
}
