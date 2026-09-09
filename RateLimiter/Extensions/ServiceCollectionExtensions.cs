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
    public static class ServiceCollectionExtensions
    {
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

        public static IServiceCollection UseAlgorithm<T>(this IServiceCollection services)
           where T : class, IAlgorithmRateLimiterFactory
        {
            services.AddSingleton<IAlgorithmRateLimiterFactory, T>();

            return services;
        }

        public static IServiceCollection UseRejectionHandler<T>(this IServiceCollection services)
          where T : class, IRateLimitRejectionHandler
        {
            services.RemoveAll<IRateLimitRejectionHandler>();
            services.AddSingleton<IRateLimitRejectionHandler, T>();

            return services;
        }

        public static IServiceCollection UseStorage<T>(this IServiceCollection services)
            where T : class, IDataStorage
        {
            services.RemoveAll<IDataStorage>();


            services.AddSingleton<IDataStorage, T>();

            return services;
        }

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

        public static IServiceCollection UseKeyProvider<T>(this IServiceCollection services)
           where T : class, IKeyProvider
        {
            services.RemoveAll<IKeyProvider>();
            services.AddSingleton<IKeyProvider, T>();

            return services;
        }

        public static IServiceCollection UseRedis(this IServiceCollection services,
           string connectionString,
           int db = 0)
        {
            services.AddRedisConnectionMultiplexerIfNotRegistered(connectionString);
            services.AddRedisStorage(db);

            return services;
        }

        public static IServiceCollection UseRedis(this IServiceCollection services,
            Action<ConfigurationOptions> configure,
            int db = 0)
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


            services.AddRedisStorage(db);

            return services;
        }

        public static IServiceCollection UseRedis(this IServiceCollection services,
            IConnectionMultiplexer multiplexer,
            int db = 0)
        {
            services.AddRedisStorage(db, multiplexer);

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
            int db,
            IConnectionMultiplexer? multiplexer = null)
        {
            services.RemoveAll<IDataStorage>();

            services.AddSingleton<IDataStorage>(sp =>
            {
                if (multiplexer == null)
                {
                    multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
                }
                var logger = sp.GetRequiredService<ILogger<RedisRateLimiterStorage>>();
                var factory = sp.GetRequiredService<IRateLimiterFactory>();

                return new RedisRateLimiterStorage(multiplexer,
                    db,
                    factory);
            });
        }

        #endregion
    }
}
