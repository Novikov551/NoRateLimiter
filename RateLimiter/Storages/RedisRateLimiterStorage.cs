using Microsoft.Extensions.Logging;
using RateLimiter.RateLimiters;
using StackExchange.Redis;

namespace RateLimiter.Storages
{
    public class RedisRateLimiterStorage : IDataStorage
    {
        private readonly IDatabase _database;
        private const string KeyPrefix = "ratelimiter:";
        private const int MaxRetries = 10;
        private readonly TimeSpan _entityTtl;
        private readonly IRateLimiterFactory _rateLimiterFactory;

        public RedisRateLimiterStorage(IConnectionMultiplexer multiplexer,
            int db,
            IRateLimiterFactory rateLimiterFactory,
            TimeSpan? stateTtl = null)
        {
            _database = multiplexer.GetDatabase(db);

            _rateLimiterFactory = rateLimiterFactory;
            _entityTtl = stateTtl ?? TimeSpan.FromMinutes(10);
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
                    ? RestoreLimiter(policyName, state)
                    : _rateLimiterFactory.Create(policyName);

            return action(rateLimiter);
        }

        private async ValueTask<T> ExecuteAsync<T>(string key,
           Func<IRateLimiter, T> action,
           CancellationToken ct = default)
        {
            var redisKey = KeyPrefix + key;
            var policyName = PolicyKeyHelper.GetPolicyName(key);

            for (var retry = 0; retry < MaxRetries; retry++)
            {
                ct.ThrowIfCancellationRequested();

                await _database.ExecuteAsync("WATCH", redisKey);

                var state = await _database.StringGetAsync(redisKey);

                IRateLimiter rateLimiter = state.HasValue
                    ? RestoreLimiter(policyName, state)
                    : _rateLimiterFactory.Create(policyName);

                var result = action(rateLimiter);

                var serizalized = SerializeLimiter(rateLimiter);

                var transaction = _database.CreateTransaction();
                _ = transaction.StringSetAsync(redisKey, serizalized, _entityTtl);

                var commited = await transaction.ExecuteAsync();
                if (commited)
                {
                    return result;
                }
            }

            throw new RedisException("Too many concurrent modifications");
        }

        private IRateLimiter RestoreLimiter(string policyName, string state)
        {
            var limiter = _rateLimiterFactory.Create(policyName);

            if(limiter is ISerializableRateLimiter serializable)
            {
                serializable.Deserialize(state);

                return limiter;
            }

            throw new InvalidOperationException($"{limiter.GetType().Name} must implement `ISerializableRateLimiter` for saved in distributed storage.");
        }

        private static string SerializeLimiter(IRateLimiter limiter)
        {
            if (limiter is ISerializableRateLimiter serializable)
            {
                return serializable.Serialize();
            }

            throw new InvalidOperationException($"{limiter.GetType().Name} must implement `ISerializableRateLimiter` for saved in distributed storage.");
        }

        #endregion
    }
}
