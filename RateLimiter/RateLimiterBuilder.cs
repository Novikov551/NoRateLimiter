using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RateLimiter.KeyProviders;
using RateLimiter.Storages;

namespace RateLimiter
{
    public class RateLimiterBuilder
    {
        internal RateLimiterOptions Options { get; }
        internal IServiceCollection Services { get; }

        internal Action<IServiceCollection> StorageRegistration { get; set; }
        internal Action<IServiceCollection> KeyProviderRegistration { get; set; }

        public RateLimiterBuilder(IServiceCollection services, RateLimiterOptions options)
        {
            Services = services;
            Options = options;

            StorageRegistration = s => s.AddSingleton<IRateLimiterStorage, InMemoryRateLimiterStorage>();

            KeyProviderRegistration = s => s.AddSingleton<IRateLimiterKeyProvider, RateLimiterUserKeyProvider>();
        }
    }
}
