namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Базовый интерфейс rate limiter'а. Определяет операции потребления
    /// ресурсов, получения остатка и времени сброса лимита.
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Попытаться потратить указанное количество токенов/запросов.
        /// </summary>
        /// <param name="tokens">Количество токенов для消耗а. По умолчанию 1.</param>
        /// <returns>true — если лимит не превышен, токены списаны. false — отклонить запрос.</returns>
        bool TryConsume(int tokens = 1);

        /// <summary>
        /// Текущее оставшееся количество токенов/запросов.
        /// </summary>
        int GetRemaining();

        /// <summary>
        /// Время, когда лимит сбросится или следующий токен станет доступен.
        /// </summary>
        DateTime GetReset();
    }
}
