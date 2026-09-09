namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Фабрика лимитеров. Создаёт экземпляры <see cref="IRateLimiter"/>
    /// по имени политики. Единственная реализация — <see cref="CompositeRateLimiterFactory"/>,
    /// которая делегирует создание алгоритм-специфичным фабрикам.
    /// </summary>
    public interface IRateLimiterFactory
    {
        /// <summary>
        /// Создать новый лимитер для указанной политики.
        /// </summary>
        /// <param name="policyName">Имя зарегистрированной политики.</param>
        IRateLimiter Create(string policyName);

        /// <summary>
        /// Получить значение Remaining по умолчанию для политики.
        /// Используется когда лимитер ещё не создан (первый запрос).
        /// Для заголовка X-RateLimit-Limit.
        /// </summary>
        int GetDefaultRemaining(string policyName);
    }
}
