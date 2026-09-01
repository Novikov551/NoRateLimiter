using Microsoft.Extensions.DependencyInjection;
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
            services.AddSingleton(options);

            return builder;
        }
    }
}
