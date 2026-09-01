using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RateLimiter.Exceptions;
using RateLimiter.Middlewares.KeyProviders;
using RateLimiter.RateLimiters;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;

namespace RateLimiter.Middlewares
{
    public class RateLimiterMiddleware : IDisposable
    {
        private readonly RequestDelegate _next;
        private readonly ConcurrentDictionary<string, Lazy<UserLastAccessDto>> _users;
        private readonly System.Timers.Timer _timer = new System.Timers.Timer(600000);//TODO
        private readonly ILogger<RateLimiterMiddleware> _logger;
        private readonly IRateLimiterKeyProvider _keyProvider;
        private readonly RateLimiterMiddlewareOptions _options;

        public RateLimiterMiddleware(RequestDelegate next,
            RateLimiterMiddlewareOptions options,
            ILogger<RateLimiterMiddleware> logger)
        {
            _options = options;
            _keyProvider = options.KeyProvider;
            _next = next;
            ValidateOptions();
            _users = new();

            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _timer.Enabled = true;

            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var now = DateTime.UtcNow;
            var key = _keyProvider.GetKey(context);
            var userLastAccessInfo = GetUserLastAccessInfo(key, now);

            SetRateLimitHeaders(context, userLastAccessInfo.Limiter);

            if (userLastAccessInfo.Limiter.TryConsume(1))
            {
                await _next(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new { message = "Rate limit exceeded" });
                await context.Response.WriteAsync(json);
            }
        }

        private void SetRateLimitHeaders(HttpContext context, IRateLimiter limiter)
        {
            var resetTime = limiter.GetReset();
            var retryAfterSeconds = Math.Max(1, (int)(resetTime - DateTime.UtcNow).TotalSeconds);

            context.Response.Headers["X-RateLimit-Limit"] = limiter.GetLimit().ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = limiter.GetRemaining().ToString();
            context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                CleanCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private void CleanCache()
        {
            foreach (var entry in _users)
            {
                if (entry.Value.Value.LastAccess < DateTime.UtcNow.AddHours(-1))
                {
                    _users.TryRemove(entry.Key, out _);//TODO
                }
            }
        }

        private UserLastAccessDto GetUserLastAccessInfo(string ipAdress, DateTime now)
        {
            var userLastAccessInfo = _users.GetOrAdd(ipAdress, 
                e => new Lazy<UserLastAccessDto>(() => new UserLastAccessDto(CreateLimiter(), now)));

            userLastAccessInfo.Value.LastAccess = now;

            return userLastAccessInfo.Value;
        }

        private void ValidateOptions()
        {
            if (_options == null)
            {
                throw new ArgumentNullException(nameof(RateLimiterMiddlewareOptions));
            }

            switch (_options.Type)
            {
                case RateLimiterType.SlidingWindow:
                    if (!_options.RequestsLimit.HasValue)
                    {
                        throw new ArgumentNullException(nameof(_options.RequestsLimit));
                    }

                    if (!_options.Window.HasValue)
                    {
                        throw new ArgumentNullException(nameof(_options.Window));
                    }

                    break;
                case RateLimiterType.TokenBucket:
                    if (!_options.RefillRate.HasValue)
                    {
                        throw new ArgumentNullException(nameof(_options.RefillRate));
                    }

                    if (!_options.Capacity.HasValue)
                    {
                        throw new ArgumentNullException(nameof(_options.Capacity));
                    }

                    break;
                default:
                    throw new UnknownRateLimiterTypeException(nameof(_options.Type));
            }
        }

        private IRateLimiter CreateLimiter()
        {
            return _options.Type switch
            {
                RateLimiterType.SlidingWindow => new SlidingWindow(_options.Window!.Value,
                        _options.RequestsLimit!.Value),
                RateLimiterType.TokenBucket => new TokenBucket(_options.RefillRate!.Value,
                        _options.Capacity!.Value),
                _ => throw new UnknownRateLimiterTypeException(nameof(_options.Type))
            };
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
