namespace RateLimiter.RateLimiters.SlidingWindow
{
    /// <summary>
    /// Фабрика для алгоритма Sliding Window. Создаёт <see cref="SlidingWindow"/>
    /// из <see cref="SlidingWindowRateLimiterPolicy"/>.
    /// </summary>
    public class SlidingWindowRateLimiterFactory : IAlgorithmRateLimiterFactory
    {

        public AlgorithmType Algorithm => AlgorithmType.SlidingWindow;

        public IRateLimiter Create(RateLimiterPolicy policy)
        {
            if (policy is not SlidingWindowRateLimiterPolicy swp)
            {
                throw new InvalidCastException($"Expected {nameof(SlidingWindowRateLimiterPolicy)}, but got {policy.GetType().Name}");
            }

            return new SlidingWindow(swp.Window, swp.RequestsLimit);
        }

        public int GetDefaultRemaining(RateLimiterPolicy policy)
        {
            if (policy is not SlidingWindowRateLimiterPolicy swp)
            {
                throw new InvalidCastException($"Expected {nameof(SlidingWindowRateLimiterPolicy)}, but got {policy.GetType().Name}");
            }

            return swp.RequestsLimit;
        }
    }
}