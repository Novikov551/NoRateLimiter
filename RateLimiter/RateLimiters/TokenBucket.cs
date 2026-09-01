namespace RateLimiter.RateLimiters
{
    public class TokenBucket : IRateLimiter
    {
        private int _tokens;
        private readonly int _refillRate;
        private readonly int _capacity;
        private DateTime _lastRefill;
        private readonly object _lock = new object();

        public TokenBucket(int refillRate,
            int capacity)
        {
            _refillRate = refillRate;
            _capacity = capacity;

            _tokens = _capacity;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryConsume(int tokens = 1)
        {
            lock (_lock)
            {
                Refill();

                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    return true;
                }

                return false;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            _tokens = (int)Math.Min(_capacity, _tokens + _refillRate * elapsed);
            _lastRefill = now;
        }

        public int GetRemaining()
        {
            lock (_lock)
            {
                Refill();
                return _tokens;
            }
        }

        public int GetLimit()
        {
            return _capacity;
        }

        public DateTime GetReset()
        {
            lock (_lock)
            {
                Refill();

                if (_tokens >= 1)
                {
                    return DateTime.UtcNow;
                }

                var nextRefill = (1.0 - _tokens) / _refillRate;
                return DateTime.UtcNow.AddSeconds(nextRefill);
            }
        }
    }
}
