using Microsoft.AspNetCore.Http;
using RateLimiter.Attributes;
using RateLimiter.KeyProviders;
using RateLimiter.RateLimiters;
using RateLimiter.Storages;

namespace RateLimiter
{
    public class RateLimiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDataStorage _storage;
        private readonly IKeyProvider _keyProvider;
        private readonly IRateLimitRejectionHandler _rejectionHandler;

        public RateLimiterMiddleware(RequestDelegate next,
            IKeyProvider keyProvider,
            IDataStorage rateLimiterStorage,
            IRateLimitRejectionHandler rejectionHandler)
        {
            _next = next;
            _keyProvider = keyProvider;
            _storage = rateLimiterStorage;
            _rejectionHandler = rejectionHandler;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<SkipRateLimitingAttribute>() != null)
            {
                await _next(context);
                return;
            }

            var policyName = endpoint?.Metadata.GetMetadata<RateLimitPolicyAttribute>()?
                .PolicyName ?? "default";

            var clientKey = _keyProvider.GetKey(context);
            var key = PolicyKeyHelper.BuildKey(policyName, clientKey);

            if (await _storage.TryConsumeAsync(key, 1))
            {
                await SetRateLimitHeadersAsync(context, key);
                await _next(context);
            }
            else
            {
                await SetRateLimitHeadersAsync(context, key);
                await _rejectionHandler.HandleAsync(context);
            }
        }

        #region Private

        private async Task SetRateLimitHeadersAsync(HttpContext context, 
            string key)
        {
            var resetTime = await _storage.GetResetAsync(key);
            var retryAfterSeconds = Math.Max(1, (int)(resetTime - DateTime.UtcNow).TotalSeconds);

            context.Response.Headers["X-RateLimit-Limit"] = (await _storage.GetLimitAsync(key)).ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (await _storage.GetRemainingAsync(key)).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        }

        #endregion
    }
}
