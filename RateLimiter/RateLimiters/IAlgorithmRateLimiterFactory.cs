namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Фабрика для одного конкретного алгоритма. <see cref="CompositeRateLimiterFactory"/>
    /// делегирует вызовы нужной фабрике на основе <see cref="Algorithm"/>.
    /// Для добавления нового алгоритма — реализуйте этот интерфейс
    /// и зарегистрируйте через <c>services.UseAlgorithm&lt;T&gt;()</c>.
    /// </summary>
    public interface IAlgorithmRateLimiterFactory
    {
        /// <summary>
        /// Тип алгоритма, за который отвечает эта фабрика.
        /// </summary>
        AlgorithmType Algorithm { get; }

        /// <summary>
        /// Создать экземпляр лимитера из политики.
        /// Реализация кастует policy к конкретному типу внутри.
        /// </summary>
        IRateLimiter Create(RateLimiterPolicy policy);

        /// <summary>
        /// Значение Remaining по умолчанию для заголовка X-RateLimit-Limit,
        /// когда лимитер ещё не создан.
        /// </summary>
        int GetDefaultRemaining(RateLimiterPolicy rateLimiterPolicy);
    }
}
