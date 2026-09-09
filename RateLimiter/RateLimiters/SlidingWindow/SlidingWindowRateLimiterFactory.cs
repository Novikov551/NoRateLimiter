namespace RateLimiter.RateLimiters.SlidingWindow
{
    public class SlidingWindowRateLimiterFactory : IAlgorithmRateLimiterFactory
    {
        public SlidingWindowRateLimiterFactory()
        {
        }

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