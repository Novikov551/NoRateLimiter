using RateLimiter.RateLimiters;
using StackExchange.Redis;

namespace RateLimiter.Storages
{
    /// <summary>
    /// Redis-хранилище состояния rate limiter'ов. Сериализует лимитеры
    /// в JSON и сохраняет в Redis. Использует optimistic locking
    /// (WATCH/MULTI/EXEC) для корректной работы в конкурентной среде.
    /// Read-only операции (GetRemaining, GetReset) не обновляют TTL.
    /// </summary>
    public class RedisRateLimiterStorage : IDataStorage
    {
        private readonly IDatabase _database;
        private const string KeyPrefix = "ratelimiter:";
        private readonly int _maxRetries;
        private readonly TimeSpan _entityTtl;
        private readonly IRateLimiterFactory _rateLimiterFactory;

        public RedisRateLimiterStorage(
            IConnectionMultiplexer multiplexer,
            IRateLimiterFactory rateLimiterFactory,
            RedisStorageOptions options)
        {
            _database = multiplexer.GetDatabase(options.Db);
            _rateLimiterFactory = rateLimiterFactory;
            _maxRetries = options.MaxRetries;
            _entityTtl = options.StateTtl ?? TimeSpan.FromMinutes(10);
        }

        public async ValueTask<DateTime> GetResetAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return await ReadAsync(key, limiter => limiter.GetReset(), ct);
        }

        public ValueTask<int> GetLimitAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return ValueTask.FromResult(_rateLimiterFactory
                .GetDefaultRemaining(
                PolicyKeyHelper.GetPolicyName(key)));
        }

        public async ValueTask<int> GetRemainingAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return await ReadAsync(key, limiter => limiter.GetRemaining(), ct);
        }

        public async ValueTask<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return await ExecuteAsync(key, limiter => limiter.TryConsume(tokens), ct);
        }

        #region Private

        private async ValueTask<T> ReadAsync<T>(string key,
           Func<IRateLimiter, T> action,
           CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var redisKey = KeyPrefix + key;
            var policyName = PolicyKeyHelper.GetPolicyName(key);

            var state = await _database.StringGetAsync(redisKey);
            IRateLimiter rateLimiter = state.HasValue
                    ? RestoreLimiter(policyName, state!)
                    : _rateLimiterFactory.Create(policyName);

            return action(rateLimiter);
        }

        private async ValueTask<T> ExecuteAsync<T>(string key,
           Func<IRateLimiter, T> action,
           CancellationToken ct = default)
        {
            var redisKey = KeyPrefix + key;
            var policyName = PolicyKeyHelper.GetPolicyName(key);

            for (var retry = 0; retry < _maxRetries; retry++)
            {
                ct.ThrowIfCancellationRequested();

                await _database.ExecuteAsync("WATCH", redisKey);

                var state = await _database.StringGetAsync(redisKey);

                IRateLimiter rateLimiter = state.HasValue
                    ? RestoreLimiter(policyName, state!)
                    : _rateLimiterFactory.Create(policyName);

                var result = action(rateLimiter);

                var serialized = SerializeLimiter(rateLimiter);

                var transaction = _database.CreateTransaction();
                _ = transaction.StringSetAsync(redisKey, serialized, _entityTtl);

                var committed = await transaction.ExecuteAsync();
                if (committed)
                {
                    return result;
                }
            }

            throw new RedisException("Too many concurrent modifications");
        }

        private IRateLimiter RestoreLimiter(string policyName, string state)
        {
            var limiter = _rateLimiterFactory.Create(policyName);

            if (limiter is ISerializableRateLimiter serializable)
            {
                serializable.Deserialize(state);

                return limiter;
            }

            throw new InvalidOperationException(
                $"{limiter.GetType().Name} must implement " +
                $"{nameof(ISerializableRateLimiter)} for distributed storage.");
        }

        private static string SerializeLimiter(IRateLimiter limiter)
        {
            if (limiter is ISerializableRateLimiter serializable)
            {
                return serializable.Serialize();
            }

            throw new InvalidOperationException(
                $"{limiter.GetType().Name} must implement " +
                $"{nameof(ISerializableRateLimiter)} for distributed storage.");
        }

        #endregion
    }
}
