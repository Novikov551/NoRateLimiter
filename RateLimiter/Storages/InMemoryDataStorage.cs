using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RateLimiter.RateLimiters;

namespace RateLimiter.Storages
{
    /// <summary>
    /// InMemory хранилище на основе <see cref="IMemoryCache"/>.
    /// Лимитеры создаются при первом запросе и хранятся в кэше
    /// с настраиваемым TTL. При удалении из кэша клиент получает
    /// полный лимит заново.
    /// </summary>
    public class InMemoryDataStorage : IDataStorage, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly IRateLimiterFactory _rateLimiterFactory;
        private readonly MemoryCacheEntryOptions _entryOptions;

        public InMemoryDataStorage(
            IRateLimiterFactory rateLimiterFactory,
            MemoryCacheOptions memoryCacheOptions,
            MemoryCacheEntryOptions memoryCacheEntryOptions)
        {
            _rateLimiterFactory = rateLimiterFactory;
            _entryOptions = memoryCacheEntryOptions;
            _cache = new MemoryCache(memoryCacheOptions);
        }

        public ValueTask<int> GetRemainingAsync(string key,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if(_cache.TryGetValue(key, out IRateLimiter? rateLimiter))
            {
                return ValueTask.FromResult(rateLimiter!.GetRemaining());
            }

            return ValueTask.FromResult(_rateLimiterFactory.GetDefaultRemaining(PolicyKeyHelper.GetPolicyName(key)));
        }

        public ValueTask<DateTime> GetResetAsync(string key, 
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (_cache.TryGetValue(key, out IRateLimiter? rateLimiter))
            {
                return ValueTask.FromResult(rateLimiter!.GetReset());
            }

            return ValueTask.FromResult(DateTime.UtcNow);
        }

        public ValueTask<int> GetLimitAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return ValueTask.FromResult(_rateLimiterFactory
                .GetDefaultRemaining(
                PolicyKeyHelper.GetPolicyName(key)));
        }

        public ValueTask<bool> TryConsumeAsync(string key,
            int tokens = 1, 
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var limiter = _cache.GetOrCreate(key, entry =>
            {
                entry.SetOptions(_entryOptions);
                return CreateLimiter(key);
            });

            return ValueTask.FromResult(limiter!.TryConsume(tokens));
        }

        public void Dispose()
        {
           _cache.Dispose();
        }

        #region Private

        private IRateLimiter CreateLimiter(string key)
        {
            return _rateLimiterFactory.Create(PolicyKeyHelper.GetPolicyName(key));
        }

        #endregion
    }
}
