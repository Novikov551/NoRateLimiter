namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Расширяет <see cref="IRateLimiter"/> сериализацией состояния.
    /// Необходим для распределённых хранилищ (Redis) — состояние лимитера
    /// сохраняется и восстанавливается между запросами.
    /// </summary>
    public interface ISerializableRateLimiter : IRateLimiter
    {
        /// <summary>
        /// Сериализовать текущее состояние лимитера в строку (JSON).
        /// </summary>
        string Serialize();

        /// <summary>
        /// Восстановить состояние лимитера из сериализованной строки.
        /// </summary>
        /// <param name="state">Строка, полученная из <see cref="Serialize"/>.</param>
        void Deserialize(string state);
    }
}
