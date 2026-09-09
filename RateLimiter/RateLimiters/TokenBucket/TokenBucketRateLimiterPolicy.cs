namespace RateLimiter.RateLimiters.TokenBucket
{
    /// <summary>
    /// Политика для алгоритма Token Bucket. Определяет ёмкость ведра,
    /// скорость пополнения и интервал.
    /// </summary>
    public class TokenBucketRateLimiterPolicy : RateLimiterPolicy
    {
        /// <summary>
        /// Максимальная ёмкость ведра (количество токенов). Должно быть > 0.
        /// </summary>
        public int TokenLimit { get; set; }

        /// <summary>
        /// Количество токенов, добавляемых за один <see cref="ReplenishmentPeriod"/>. Должно быть > 0.
        /// </summary>
        public int TokensPerPeriod { get; set; }

        /// <summary>
        /// Интервал пополнения токенов. Должен быть > TimeSpan.Zero.
        /// </summary>
        public TimeSpan ReplenishmentPeriod { get; set; }

        public override AlgorithmType Algorithm => AlgorithmType.TokenBucket;
    }
}
