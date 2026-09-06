using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RateLimiter.Exceptions;
using RateLimiter.RateLimiters;

namespace RateLimiter.Storages.InMemory
{
    public class InMemoryDataStorage : IDataStorage, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<InMemoryDataStorage> _logger;
        private readonly RateLimiterOptions _options;
        private readonly MemoryCacheEntryOptions _entryOptions;

        public InMemoryDataStorage(
            RateLimiterOptions options,
            MemoryCacheOptions memoryCacheOptions,
            MemoryCacheEntryOptions memoryCacheEntryOptions,
            ILogger<InMemoryDataStorage> logger)
        {
            _entryOptions = memoryCacheEntryOptions;
            _options = options;
            _logger = logger;
            _cache = new MemoryCache(memoryCacheOptions);
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

        public int GetLimit(string key)
        {
            if (_cache.TryGetValue(key, out IRateLimiter? rateLimiter))
            {
                return rateLimiter!.GetLimit();
            }

            return _options.Type switch
            {
                RateLimiterType.SlidingWindow => _options.RequestsLimit!.Value,
                RateLimiterType.TokenBucket => _options.Capacity!.Value,
                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type)),
            };
        }

        public Task<int> GetRemainingAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if(_cache.TryGetValue(key, out IRateLimiter? rateLimiter))
            {
                return Task.FromResult(rateLimiter!.GetRemaining());
            }

            return Task.FromResult(_options.Type switch
            {
                RateLimiterType.SlidingWindow => _options.RequestsLimit!.Value,
                RateLimiterType.TokenBucket => _options.Capacity!.Value,
                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type)),
            });
        }

        public Task<DateTime> GetResetAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (_cache.TryGetValue(key, out IRateLimiter? rateLimiter))
            {
                return Task.FromResult(rateLimiter!.GetReset());
            }

            return Task.FromResult(DateTime.UtcNow);
        }

        public Task<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var limiter = _cache.GetOrCreate(key, entry =>
            {
                entry.SetOptions(_entryOptions);
                return CreateLimiter();
            });

            return Task.FromResult(limiter!.TryConsume(tokens));
        }

        public void Dispose()
        {
           _cache.Dispose();
        }
    }
}
