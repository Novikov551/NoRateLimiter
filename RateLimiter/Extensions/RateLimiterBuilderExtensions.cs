using Microsoft.Extensions.DependencyInjection;
using RateLimiter.KeyProviders;

namespace RateLimiter.Extensions
{
    public static class RateLimiterBuilderExtensions
    {
        public static RateLimiterBuilder UseStorage<T>(this RateLimiterBuilder builder)
            where T : class, IRateLimiterStorage
        {
            builder.StorageRegistration = s => s.AddSingleton<IRateLimiterStorage, T>();

            return builder;
        }

        public static RateLimiterBuilder UseKeyProvider<T>(this RateLimiterBuilder builder)
            where T : class, IRateLimiterKeyProvider
        {
            builder.KeyProviderRegistration = s => s.AddSingleton<IRateLimiterKeyProvider, T>();

            return builder;
        }

        internal static void Build(this RateLimiterBuilder builder)
        {
            builder.StorageRegistration(builder.Services);
            builder.KeyProviderRegistration(builder.Services);
        }
    }
}
