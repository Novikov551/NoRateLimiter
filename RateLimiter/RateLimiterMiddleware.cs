using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RateLimiter.KeyProviders;
using System.Text.Json;

namespace RateLimiter
{
    public class RateLimiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimiterMiddleware> _logger;
        private readonly IRateLimiterStorage _storage;
        private readonly IRateLimiterKeyProvider _keyProvider;

        public RateLimiterMiddleware(RequestDelegate next,
            IRateLimiterKeyProvider keyProvider,
            IRateLimiterStorage rateLimiterStorage,
            ILogger<RateLimiterMiddleware> logger)
        {
            _next = next;
            _keyProvider = keyProvider;
            _storage = rateLimiterStorage;

            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var now = DateTime.UtcNow;
            var key = _keyProvider.GetKey(context);

            if (_storage.TryConsume(key, 1))
            {
                SetRateLimitHeaders(context, key);
                await _next(context);
            }
            else
            {
                SetRateLimitHeaders(context, key);
                _logger.LogWarning("Rate limit exceeded. Key: {Key}", key);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new { message = "Rate limit exceeded" });
                await context.Response.WriteAsync(json);
            }
        }

        private void SetRateLimitHeaders(HttpContext context, string key)
        {
            var resetTime = _storage.GetReset(key);
            var retryAfterSeconds = Math.Max(1, (int)(resetTime - DateTime.UtcNow).TotalSeconds);

            context.Response.Headers["X-RateLimit-Limit"] = _storage.GetLimit(key).ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = _storage.GetRemaining(key).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        }
    }
}
