namespace RateLimiter.RateLimiters.SlidingWindow
{
    /// <summary>
    /// Политика для алгоритма Sliding Window. Определяет максимум запросов
    /// в скользящем временном окне.
    /// </summary>
    public class SlidingWindowRateLimiterPolicy : RateLimiterPolicy
    {
        /// <summary>
        /// Максимальное количество запросов в окне. Должно быть > 0.
        /// </summary>
        public int RequestsLimit { get; set; }

        /// <summary>
        /// Длительность скользящего окна. Должно быть > TimeSpan.Zero.
        /// </summary>
        public TimeSpan Window { get; set; }

        public override AlgorithmType Algorithm => AlgorithmType.SlidingWindow;
    }
}
