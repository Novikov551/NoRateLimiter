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
        internal Dictionary<string, RateLimiterPolicy> Policies { get; set; } = new Dictionary<string, RateLimiterPolicy>();
    }
}
