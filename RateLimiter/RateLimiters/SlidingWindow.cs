namespace RateLimiter.RateLimiters
{
    public class SlidingWindow : IRateLimiter
    {
        private readonly TimeSpan _window;
        private readonly List<DateTime> _requests;
        private readonly int _maxRequests;
        private readonly object _lock = new object();

        public SlidingWindow(TimeSpan window,
            int maxRequests)
        {
            _window = window;
            _requests = new List<DateTime>();
            _maxRequests = maxRequests;
        }

        public bool TryConsume(int tokens = 1)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                ClearWindow(now);

                if (_requests.Count + tokens > _maxRequests)
                {
                    return false;
                }

                for (var i = 0; i < tokens; i++)
                {
                    _requests.Add(now);
                }

                return true;
            }
        }

        private void ClearWindow(DateTime now)
        {
            _requests.RemoveAll(e => e < now - _window);
        }

        public int GetRemaining()
        {
            lock (_lock)
            {
                ClearWindow(DateTime.UtcNow);
                return Math.Max(0, _maxRequests - _requests.Count);
            }
        }

        public int GetLimit()
        {
            return _maxRequests;
        }

        public DateTime GetReset()
        {
            lock (_lock)
            {
                ClearWindow(DateTime.UtcNow);

                if(_requests.Count < _maxRequests)
                {
                    return DateTime.UtcNow;
                }

                return _requests[0] + _window;
            }
        }
    }
}
