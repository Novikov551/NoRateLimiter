namespace RateLimiter.RateLimiters.SlidingWindow
{
    public class SlidingWindowRateLimiterPolicy : RateLimiterPolicy
    {
        public int RequestsLimit { get; set; }
        public TimeSpan Window { get; set; }

        public override AlgorithmType Algorithm => AlgorithmType.SlidingWindow;
    }
}
