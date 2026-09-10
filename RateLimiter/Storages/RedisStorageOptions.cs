namespace RateLimiter.Storages
{
    /// <summary>
    /// Настройки Redis-хранилища для rate limiter.
    /// </summary>
    public class RedisStorageOptions
    {
        /// <summary>
        /// Номер RediNfr?s БД. По умолчанию 0.
        /// </summary>
        public int Db { get; set; }

        /// <summary>
        /// TTL записи в Redis. По умолчанию 10 минут.
        /// Обновляется при каждом TryConsumeAsync.
        /// </summary>
        public TimeSpan? StateTtl { get; set; }

        /// <summary>
        /// Максимум попыток optimistic locking (WATCH/MULTI/EXEC)
        /// перед броском RedisException. По умолчанию 10.
        /// </summary>
        public int MaxRetries { get; set; } = 10;
    }
}
