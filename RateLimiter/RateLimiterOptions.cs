using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RateLimiter
{
    public class RateLimiterOptions : IValidatableObject
    {
        public RateLimiterType Type { get; set; } = RateLimiterType.TokenBucket;
        public int? RequestsLimit { get; set; } = null;
        public TimeSpan? Window { get; set; } = null;
        public int? Capacity { get; set; } = 20;
        public int? RefillRate { get; set; } = 2;

        public Func<HttpContext, Task>? OnRejected { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            switch (Type)
            {
                case RateLimiterType.SlidingWindow:
                    if (!RequestsLimit.HasValue)
                    {
                        yield return new ValidationResult(
                             $"For the `{nameof(RateLimiterType.SlidingWindow)}` algorithm, a request limit must be specified.", new[] { nameof(RequestsLimit) } );
                    }

                    if (!Window.HasValue)
                    {
                        yield return new ValidationResult(
                             $"For the `{nameof(RateLimiterType.SlidingWindow)}` algorithm, you must specify a time window for calculating the number of requests.", new[] { nameof(Window) });
                    }

                    break;

                case RateLimiterType.TokenBucket:
                    if (!RefillRate.HasValue)
                    {
                        yield return new ValidationResult(
                             $"For the `{nameof(RateLimiterType.TokenBucket)}` algorithm, you must specify the rate at which available requests are replenished.", new[] { nameof(RefillRate) });
                    }

                    if (!Capacity.HasValue)
                    {
                        yield return new ValidationResult(
                             $"For the `{nameof(RateLimiterType.TokenBucket)}` algorithm, the maximum number of requests must be specified.", new[] { nameof(Capacity) });
                    }

                    break;

                default:
                    yield return new ValidationResult(
                             $"Unknown request-limiting algorithm `{Type}` specified.", new[] { nameof(Type) });
                    break;
            }
        }
    }
}
