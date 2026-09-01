using Microsoft.Extensions.Logging;
using RateLimiter.Exceptions;
using RateLimiter.RateLimiters;
using System.Collections.Concurrent;
using System.Timers;

namespace RateLimiter.Storages
{
    public class InMemoryRateLimiterStorage : IRateLimiterStorage, IDisposable
    {
        private readonly ConcurrentDictionary<string, Lazy<UserLastAccessDto>> _users;
        private readonly System.Timers.Timer _timer = new System.Timers.Timer(600000);//TODO
        private readonly ILogger<InMemoryRateLimiterStorage> _logger;
        private readonly RateLimiterOptions _options;

        public InMemoryRateLimiterStorage(
            RateLimiterOptions options,
            ILogger<InMemoryRateLimiterStorage> logger)
        {
            _users = new();

            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _options = options;
            _logger = logger;
        }


        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                CleanCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private void CleanCache()
        {
            foreach (var entry in _users)
            {
                if (entry.Value.Value.LastAccess < DateTime.UtcNow.AddHours(-1))
                {
                    _users.TryRemove(entry.Key, out _);//TODO
                }
            }
        }

        private IRateLimiter CreateLimiter()
        {
            return _options.Type switch
            {
                RateLimiterType.SlidingWindow => new SlidingWindow(_options.Window!.Value,
                        _options.RequestsLimit!.Value),
                RateLimiterType.TokenBucket => new TokenBucket(_options.RefillRate!.Value,
                        _options.Capacity!.Value),
                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type))
            };
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private UserLastAccessDto? TryGetUserLastAccessInfo(string ipAdress, DateTime now)
        {
            var result = _users.TryGetValue(ipAdress, out var lastAccessInfo);
            if (result)
            {
                return lastAccessInfo.Value;
            }

            return null;
        }

        private UserLastAccessDto GetOrAddUserLastAccessInfo(string ipAdress, DateTime now)
        {
            var userLastAccessInfo = _users.GetOrAdd(ipAdress,
                e => new Lazy<UserLastAccessDto>(() => new UserLastAccessDto(CreateLimiter(), now)));

            userLastAccessInfo.Value.LastAccess = now;

            return userLastAccessInfo.Value;
        }

        public int GetLimit(string key)
        {
            var userLastAccess = TryGetUserLastAccessInfo(key, DateTime.UtcNow);
            if (userLastAccess == null)
            {
                return _options.Type switch
                {
                    RateLimiterType.SlidingWindow => _options.RequestsLimit!.Value,
                    RateLimiterType.TokenBucket => _options.Capacity!.Value,
                    _ => _options.Capacity!.Value
                };
            }

            return userLastAccess.Limiter.GetLimit();
        }

        public int GetRemaining(string key)
        {
            var userLastAccess = TryGetUserLastAccessInfo(key, DateTime.UtcNow);
            if (userLastAccess == null)
            {
                return _options.Type switch
                {
                    RateLimiterType.SlidingWindow => _options.RequestsLimit!.Value,
                    RateLimiterType.TokenBucket => _options.Capacity!.Value,
                    _ => _options.Capacity!.Value
                };
            }

            return userLastAccess.Limiter.GetRemaining();
        }

        public DateTime GetReset(string key)
        {
            var userLastAccess = TryGetUserLastAccessInfo(key, DateTime.UtcNow);
            if (userLastAccess == null)
            {
                return DateTime.UtcNow;
            }

            return userLastAccess.Limiter.GetReset();
        }

        public bool TryConsume(string key, int tokens = 1)
        {
            var userLastAccess = GetOrAddUserLastAccessInfo(key, DateTime.UtcNow);

            if (userLastAccess.Limiter.TryConsume(tokens))
            {
                return true;
            }

            return false;
        }
    }
}
