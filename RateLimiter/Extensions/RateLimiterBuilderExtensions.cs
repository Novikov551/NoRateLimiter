using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RateLimiter.KeyProviders;
using RateLimiter.Storages;
using RateLimiter.Storages.InMemory;
using StackExchange.Redis;

namespace RateLimiter.Extensions
{
    public static class RateLimiterBuilderExtensions
    {
        public static RateLimiterBuilder UseStorage<T>(this RateLimiterBuilder builder)
            where T : class, IDataStorage
        {
            var existing = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(IDataStorage));
            if (existing != null)
            {
                builder.Services.Remove(existing);
            }

            builder.Services.AddSingleton<IDataStorage, T>();

            return builder;
        }

        public static RateLimiterBuilder UseInMemoryStorage(this RateLimiterBuilder builder,
            Action<MemoryCacheOptions>? configureOptions = null,
            Action<MemoryCacheEntryOptions>? configureEntryOptions = null)
        {
            var existing = builder.Services.FirstOrDefault(e => e.ServiceType == typeof(IDataStorage));
            if (existing != null)
            {
                builder.Services.Remove(existing);
            }

            var cacheOptions = new MemoryCacheOptions() { SizeLimit = 10_000 };
            configureOptions?.Invoke(cacheOptions);

            var entryOptions = new MemoryCacheEntryOptions() { 
                SlidingExpiration = TimeSpan.FromHours(1),
                Size = 1
            };
            configureEntryOptions?.Invoke(entryOptions);

            builder.Services.AddSingleton(sp =>
            {
                return new InMemoryDataStorage(builder.Options,
                    cacheOptions,
                    entryOptions,
                    sp.GetRequiredService<ILogger<InMemoryDataStorage>>());
            });

            builder.Services.AddSingleton<IDataStorage>(sp => sp.GetRequiredService<InMemoryDataStorage>());

            return builder;
        }

        public static RateLimiterBuilder UseKeyProvider<T>(this RateLimiterBuilder builder)
            where T : class, IKeyProvider
        {
            var existing = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(IKeyProvider));
            if (existing != null)
            {
                builder.Services.Remove(existing);
            }

            builder.Services.AddSingleton<IKeyProvider, T>();

            return builder;
        }

        public static RateLimiterBuilder UseRedis(this RateLimiterBuilder builder,
            string connectionString,
            int db = 0)
        {
            builder.Services.AddRedisConnectionMultiplexerIfNotRegistered(connectionString);
            builder.Services.AddRedisStorage(builder.Options, db);

            return builder;
        }

        public static RateLimiterBuilder UseRedis(this RateLimiterBuilder builder,
            Action<ConfigurationOptions> configure,
            int db = 0)
        {
            if (!builder.Services.Any(e => e.ServiceType == typeof(IConnectionMultiplexer)))
            {
                builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var options = new ConfigurationOptions();
                    configure(options);

                    return ConnectionMultiplexer.Connect(options);
                });
            }


            builder.Services.AddRedisStorage(builder.Options, db);

            return builder;
        }

        public static RateLimiterBuilder UseRedis(this RateLimiterBuilder builder,
            IConnectionMultiplexer multiplexer,
            int db = 0)
        {
            builder.Services.AddRedisStorage(builder.Options, db, multiplexer);

            return builder;
        }

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
            RateLimiterOptions rateLimiterOptions,
            int db,
            IConnectionMultiplexer? multiplexer = null)
        {
            services.AddSingleton<IDataStorage>(sp =>
            {
                if (multiplexer == null)
                {
                    multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
                }
                var logger = sp.GetRequiredService<ILogger<RedisRateLimiterStorage>>();

                return new RedisRateLimiterStorage(multiplexer, rateLimiterOptions, logger, db);
            });
        }
    }
}
