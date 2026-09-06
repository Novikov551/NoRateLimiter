using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RateLimiter.KeyProviders;
using RateLimiter.Storages.InMemory;
using System.ComponentModel.DataAnnotations;

namespace RateLimiter.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static RateLimiterBuilder AddRateLimiter(this IServiceCollection services,
            Action<RateLimiterOptions> configure)
        {
            var options = new RateLimiterOptions();

            if (configure != null)
            {
                configure(options);
            }

            var validationsResult = options.Validate(new ValidationContext(options))
                .ToList();
            if (validationsResult.Any())
            {
                throw new ValidationException(string.Join("; ", validationsResult.Select(e => e.ErrorMessage)));
            }

            var builder = new RateLimiterBuilder(services, options);
            services.AddSingleton(builder);

            services.AddSingleton(sp =>
            {
                return new InMemoryDataStorage(options,
                    new MemoryCacheOptions() { SizeLimit = 10_000 },
                    new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromHours(1),
                        Size = 1
                    },
                    sp.GetRequiredService<ILogger<InMemoryDataStorage>>());
            });
            services.AddSingleton<IDataStorage>(sp => sp.GetRequiredService<InMemoryDataStorage>());

            services.AddSingleton<IKeyProvider, IpKeyProvider>();

            return builder;
        }
    }
}
