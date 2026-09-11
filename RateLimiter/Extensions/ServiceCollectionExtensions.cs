using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RateLimiter.KeyProviders;
using RateLimiter.RateLimiters;
using RateLimiter.RateLimiters.SlidingWindow;
using RateLimiter.RateLimiters.TokenBucket;
using RateLimiter.Storages;
using StackExchange.Redis;

namespace RateLimiter.Extensions
{
    /// <summary>
    /// Extension-методы для регистрации rate limiter в DI-контейнере.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Зарегистрировать rate limiter с указанными политиками.
        /// По умолчанию: InMemory хранилище, IP-идентификация, JSON-ответ 429.
        /// Бросает <see cref="InvalidOperationException"/> если нет политик.
        /// </summary>
        public static IServiceCollection AddRateLimiter(this IServiceCollection services,
            Action<RateLimiterOptions> configure)
        {
            var options = new RateLimiterOptions();
            configure(options);

            if (options.Policies.Count == 0)
            {
                throw new InvalidOperationException("At least one rate limiter policy must be configured.");
            }

            services.AddSingleton<IRateLimitRejectionHandler, DefaultRateLimitRejectionHandler>();

            services.AddSingleton<IAlgorithmRateLimiterFactory, TokenBucketRateLimiterFactory>();
            services.AddSingleton<IAlgorithmRateLimiterFactory, SlidingWindowRateLimiterFactory>();

            services.AddSingleton<IRateLimiterFactory>(sp =>
            {
                var factories = sp.GetRequiredService<IEnumerable<IAlgorithmRateLimiterFactory>>();

                return new CompositeRateLimiterFactory(factories, options);
            });

            services.AddSingleton<IDataStorage>(sp =>
            {
                return new InMemoryDataStorage(sp.GetRequiredService<IRateLimiterFactory>(),
                    new MemoryCacheOptions() { SizeLimit = 10_000 },
                    new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromHours(1),
                        Size = 1
                    });
            });

            services.AddSingleton<IKeyProvider, IpKeyProvider>();

            return services;
        }

        /// <summary>
        /// Зарегистрировать кастомную фабрику алгоритма. Вызывайте для каждого
        /// нового алгоритма поверх встроенных TokenBucket и SlidingWindow.
        /// </summary>
        public static IServiceCollection UseAlgorithm<T>(this IServiceCollection services)
           where T : class, IAlgorithmRateLimiterFactory
        {
            services.AddSingleton<IAlgorithmRateLimiterFactory, T>();

            return services;
        }

        /// <summary>
        /// Заменить дефолтный обработчик 429 на кастомную реализацию.
        /// </summary>
        public static IServiceCollection UseRejectionHandler<T>(this IServiceCollection services)
          where T : class, IRateLimitRejectionHandler
        {
            services.RemoveAll<IRateLimitRejectionHandler>();
            services.AddSingleton<IRateLimitRejectionHandler, T>();

            return services;
        }

        /// <summary>
        /// Заменить хранилище на кастомную реализацию <see cref="IDataStorage"/>.
        /// </summary>
        public static IServiceCollection UseStorage<T>(this IServiceCollection services)
            where T : class, IDataStorage
        {
            services.RemoveAll<IDataStorage>();

            services.AddSingleton<IDataStorage, T>();

            return services;
        }

        /// <summary>
        /// Настроить InMemory хранилище с кастомными параметрами кэша
        /// (SizeLimit, TTL и т.д.).
        /// </summary>
        public static IServiceCollection UseInMemoryStorage(this IServiceCollection services,
           Action<MemoryCacheOptions>? configureOptions = null,
           Action<MemoryCacheEntryOptions>? configureEntryOptions = null)
        {
            services.RemoveAll<IDataStorage>();

            var cacheOptions = new MemoryCacheOptions() { SizeLimit = 10_000 };
            configureOptions?.Invoke(cacheOptions);

            var entryOptions = new MemoryCacheEntryOptions()
            {
                SlidingExpiration = TimeSpan.FromHours(1),
                Size = 1
            };
            configureEntryOptions?.Invoke(entryOptions);

            services.AddSingleton<IDataStorage>(sp =>
            {
                return new InMemoryDataStorage(
                    sp.GetRequiredService<IRateLimiterFactory>(),
                    cacheOptions,
                    entryOptions);
            });

            return services;
        }

        /// <summary>
        /// Заменить дефолтный идентификатор клиента (IP) на кастомный.
        /// </summary>
        public static IServiceCollection UseKeyProvider<T>(this IServiceCollection services)
           where T : class, IKeyProvider
        {
            services.RemoveAll<IKeyProvider>();
            services.AddSingleton<IKeyProvider, T>();

            return services;
        }

        /// <summary>
        /// Подключить Redis-хранилище по строке подключения.
        /// Если <see cref="IConnectionMultiplexer"/> уже зарегистрован в DI —
        /// использует существующий.
        /// </summary>
        /// <param name="connectionString">Строка подключения к Redis.</param>
        /// <param name="configure">Опциональная настройка <see cref="RedisStorageOptions"/> (БД, TTL, ретраи).</param>
        public static IServiceCollection UseRedis(this IServiceCollection services,
           string connectionString,
           Action<RedisStorageOptions>? configure = null)
        {
            var redisOptions = new RedisStorageOptions();
            configure?.Invoke(redisOptions);

            services.AddRedisConnectionMultiplexerIfNotRegistered(connectionString);
            services.AddRedisStorage(redisOptions);

            return services;
        }

        /// <summary>
        /// Подключить Redis-хранилище с полной конфигурацией подключения
        /// (пароли, SSL, таймауты, sentinel, cluster).
        /// </summary>
        public static IServiceCollection UseRedis(this IServiceCollection services,
            Action<ConfigurationOptions> configure,
            Action<RedisStorageOptions>? configureStorage = null)
        {
            if (!services.Any(e => e.ServiceType == typeof(IConnectionMultiplexer)))
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var options = new ConfigurationOptions();
                    configure(options);

                    return ConnectionMultiplexer.Connect(options);
                });
            }

            var redisOptions = new RedisStorageOptions();
            configureStorage?.Invoke(redisOptions);

            services.AddRedisStorage(redisOptions);

            return services;
        }

        /// <summary>
        /// Подключить Redis-хранилище с существующим <see cref="IConnectionMultiplexer"/>.
        /// </summary>
        public static IServiceCollection UseRedis(this IServiceCollection services,
            IConnectionMultiplexer multiplexer,
            Action<RedisStorageOptions>? configure = null)
        {
            var redisOptions = new RedisStorageOptions();
            configure?.Invoke(redisOptions);

            services.AddRedisStorage(redisOptions, multiplexer);

            return services;
        }

        #region Private

        private static void AddRedisConnectionMultiplexerIfNotRegistered(this IServiceCollection services,
            string connectionString)
        {
            if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
            {
                return;
            }

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()?
                    .CreateLogger("RateLimiter:Redis");

                logger?.LogInformation("Creating Redis connection to {ConnectionString}", connectionString);

                return ConnectionMultiplexer.Connect(connectionString);
            });
        }

        private static void AddRedisStorage(this IServiceCollection services,
            RedisStorageOptions redisOptions,
            IConnectionMultiplexer? multiplexer = null)
        {
            services.RemoveAll<IDataStorage>();

            services.AddSingleton<IDataStorage>(sp =>
            {
                var resolvedMultiplexer = multiplexer ?? sp.GetRequiredService<IConnectionMultiplexer>();

                var factory = sp.GetRequiredService<IRateLimiterFactory>();

                return new RedisRateLimiterStorage(resolvedMultiplexer,
                    factory,
                    redisOptions);
            });
        }

        #endregion
    }
}
