using Microsoft.Extensions.Logging;
using RateLimiter.Exceptions;
using StackExchange.Redis;
using System.Threading;

namespace RateLimiter.Storages
{
    public class RedisRateLimiterStorage : IDataStorage
    {
        private readonly RateLimiterOptions _options;
        private readonly IDatabase _database;

        public RedisRateLimiterStorage(IConnectionMultiplexer multiplexer, 
            RateLimiterOptions options,
            ILogger<RedisRateLimiterStorage> logger, 
            int db)
        {
            _options = options;
            _database = multiplexer.GetDatabase(db);
        }

        public int GetLimit(string key)
        {
            return _options.Type switch
            {
                RateLimiterType.TokenBucket => _options.Capacity!.Value,
                RateLimiterType.SlidingWindow => _options.RequestsLimit!.Value,
                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type))
            };
        }

        public async Task<DateTime> GetResetAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return _options.Type switch
            {
                RateLimiterType.TokenBucket => await GetTokenBucketResetResultAsync(key, ct),
                RateLimiterType.SlidingWindow => await GetSlidingWindowResetResultAsync(key, ct),
                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type))
            };
        }

        private async Task<DateTime> GetSlidingWindowResetResultAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var result = (double)await _database.ScriptEvaluateAsync(LoadLuaScript("sliding_window_get_reset"), 
                [key], 
                [_options.Window!.Value.TotalSeconds]);
            return DateTime.UnixEpoch.AddSeconds(result).ToUniversalTime();
        }

        public async Task<int> GetRemainingAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return _options.Type switch
            {
                RateLimiterType.TokenBucket => (int)await _database.ScriptEvaluateAsync(LoadLuaScript("token_bucket_refill"), 
                [key], 
                [_options.Capacity, 
                    _options.RefillRate,
                    ( _options.Capacity!.Value / _options.RefillRate!.Value + 10)]),

                RateLimiterType.SlidingWindow => (int)await _database.ScriptEvaluateAsync(LoadLuaScript("sliding_window_get_remaining"), 
                [key],
                [_options.RequestsLimit!.Value, 
                    _options.Window!.Value.TotalSeconds]),

                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type))
            };
        }

        public async Task<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return _options.Type switch
            {
                RateLimiterType.TokenBucket => (bool)await _database.ScriptEvaluateAsync(LoadLuaScript("token_bucket_try_consume"),
                [key],
                [_options.Capacity,
                    _options.RefillRate,
                    tokens,
                    _options.Capacity!.Value/_options.RefillRate!.Value + 10]),

                RateLimiterType.SlidingWindow => (bool)await _database.ScriptEvaluateAsync(LoadLuaScript("sliding_window_try_consume"),
                [key],
                [_options.RequestsLimit!.Value, 
                    _options.Window!.Value.TotalSeconds, 
                    tokens]),

                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type))
            };
        }

        #region Private

        private static string LoadLuaScript(string name)
        {
            var assembly = typeof(RedisRateLimiterStorage).Assembly;
            var resourceName = $"RateLimiter.LuaScripts.{name}.lua";

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embeded resource '{resourceName}' not found");

            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

        private async Task<DateTime> GetTokenBucketResetResultAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var tokens = (int)await _database.ScriptEvaluateAsync(LoadLuaScript("token_bucket_refill"),
                [key],
                [_options.Capacity,
                    _options.RefillRate,
                    _options.Capacity!.Value / _options.RefillRate!.Value + 10]);

            if (tokens >= 1)
            {
                return DateTime.UtcNow;
            }

            var nextRefill = (1.0 - tokens) / _options.RefillRate!.Value;
            return DateTime.UtcNow.AddSeconds(nextRefill);
        }

        #endregion
    }
}
