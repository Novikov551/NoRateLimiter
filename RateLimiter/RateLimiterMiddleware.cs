using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RateLimiter.KeyProviders;
using System.Text.Json;
using System.Threading.Tasks;

namespace RateLimiter
{
    public class RateLimiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimiterMiddleware> _logger;
        private readonly IDataStorage _storage;
        private readonly IKeyProvider _keyProvider;

        public RateLimiterMiddleware(RequestDelegate next,
            IKeyProvider keyProvider,
            IDataStorage rateLimiterStorage,
            ILogger<RateLimiterMiddleware> logger)
        {
            _next = next;
            _keyProvider = keyProvider;
            _storage = rateLimiterStorage;

            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var key = _keyProvider.GetKey(context);

            if (await _storage.TryConsumeAsync(key, 1))
            {
                await SetRateLimitHeadersAsync(context, key);
                await _next(context);
            }
            else
            {
                await SetRateLimitHeadersAsync(context, key);
                _logger.LogWarning("Rate limit exceeded. Key: {Key}", key);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new { message = "Rate limit exceeded" });
                await context.Response.WriteAsync(json);
            }
        }

        private async Task SetRateLimitHeadersAsync(HttpContext context, string key)
        {
            var resetTime = await _storage.GetResetAsync(key);
            var retryAfterSeconds = Math.Max(1, (int)(resetTime - DateTime.UtcNow).TotalSeconds);

            context.Response.Headers["X-RateLimit-Limit"] = _storage.GetLimit(key).ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (await _storage.GetRemainingAsync(key)).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        }
    }
}
