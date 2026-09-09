namespace RateLimiter.Storages
{
    /// <summary>
    /// Хранилище состояния rate limiter'ов. Middleware работает
    /// только с этим интерфейсом — не знает ни алгоритмов, ни Options.
    /// Встроенные реализации: <see cref="InMemoryDataStorage"/>,
    /// <see cref="RedisRateLimiterStorage"/>.
    /// </summary>
    public interface IDataStorage
    {
        /// <summary>
        /// Попытаться потратить токен/запрос. Если лимитер не существует —
        /// создаётся через фабрику.
        /// </summary>
        /// <param name="key">Ключ в формате <c>{policyName}:{clientKey}</c>.</param>
        /// <param name="tokens">Количество токенов для消耗а.</param>
        /// <returns>true — лимит не превышен. false — отклонить.</returns>
        ValueTask<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default);

        /// <summary>
        /// Текущее оставшееся количество токенов/запросов.
        /// Для заголовка X-RateLimit-Remaining.
        /// </summary>
        ValueTask<int> GetRemainingAsync(string key, CancellationToken ct = default);

        /// <summary>
        /// Общий лимит (ёмкость) для данной политики.
        /// Для заголовка X-RateLimit-Limit.
        /// </summary>
        ValueTask<int> GetLimitAsync(string key, CancellationToken ct = default);

        /// <summary>
        /// Время сброса лимита. Для заголовков X-RateLimit-Reset и Retry-After.
        /// </summary>
        ValueTask<DateTime> GetResetAsync(string key, CancellationToken ct = default);
    }
}
