namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Базовый класс для всех политик rate limiting. Содержит имя политики
    /// и абстрактное свойство <see cref="Algorithm"/>, которое каждый наследник
    /// связывает с конкретным алгоритмом.
    /// </summary>
    public abstract class RateLimiterPolicy : IAlgorithmPolicy
    {
        /// <summary>
        /// Имя политики. Устанавливается через <c>AddTokenBucketLimiter(name, ...)</c> и т.д.
        /// Используется как часть ключа в хранилище: <c>{Name}:{clientKey}</c>.
        /// </summary>
        public string Name { get; internal set; } = string.Empty;

        /// <summary>
        /// Тип алгоритма для этой политики. Определяет, какая фабрика
        /// будет создавать лимитер.
        /// </summary>
        public abstract AlgorithmType Algorithm { get; }
    }
}
