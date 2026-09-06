using Microsoft.Extensions.Logging;
using RateLimiter.Exceptions;
using StackExchange.Redis;

namespace RateLimiter.Storages
{
    public class RedisRateLimiterStorage : IDataStorage
    {
        private readonly RateLimiterOptions _options;
        private readonly IDatabase _database;
        private readonly string _slidingWindowGetReset;
        private readonly string _tokenBucketRefill;
        private readonly string _slidingWindowGetRemaining;
        private readonly string _slidingWindowTryConsume;
        private readonly string _tokenBucketTryConsume;
        private readonly double? _tokenGenerationRate;
        private readonly ILogger<RedisRateLimiterStorage> _logger;

        public RedisRateLimiterStorage(IConnectionMultiplexer multiplexer,
            RateLimiterOptions options,
            ILogger<RedisRateLimiterStorage> logger,
            int db)
        {
            _options = options;
            _database = multiplexer.GetDatabase(db);

            _slidingWindowGetReset = LoadLuaScript("sliding_window_get_reset");
            _tokenBucketRefill = LoadLuaScript("token_bucket_refill");
            _slidingWindowGetRemaining = LoadLuaScript("sliding_window_get_remaining");
            _slidingWindowTryConsume = LoadLuaScript("sliding_window_try_consume");
            _tokenBucketTryConsume = LoadLuaScript("token_bucket_try_consume");

            if (_options.Type == RateLimiterType.TokenBucket)
            {
                _tokenGenerationRate = (double)_options.Capacity!.Value / _options.RefillRate!.Value + 10;
            }

            _logger = logger;
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

            var result = (double)await _database.ScriptEvaluateAsync(_slidingWindowGetReset,
                [key],
                [_options.Window!.Value.TotalSeconds]);
            return DateTime.UnixEpoch.AddSeconds(result).ToUniversalTime();
        }

        public async Task<int> GetRemainingAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return _options.Type switch
            {
                RateLimiterType.TokenBucket => (int)await _database.ScriptEvaluateAsync(_tokenBucketRefill,
                [key],
                [_options.Capacity,
                    _options.RefillRate,
                    _tokenGenerationRate]),

                RateLimiterType.SlidingWindow => (int)await _database.ScriptEvaluateAsync(_slidingWindowGetRemaining,
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
                RateLimiterType.TokenBucket => (bool)await _database.ScriptEvaluateAsync(_tokenBucketTryConsume,
                [key],
                [_options.Capacity,
                    _options.RefillRate,
                    tokens,
                    _tokenGenerationRate]),

                RateLimiterType.SlidingWindow => (bool)await _database.ScriptEvaluateAsync(_slidingWindowTryConsume,
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
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found");

            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

        private async Task<DateTime> GetTokenBucketResetResultAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var tokens = (int)await _database.ScriptEvaluateAsync(_tokenBucketRefill,
                [key],
                [_options.Capacity,
                    _options.RefillRate,
                    _tokenGenerationRate]);

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
