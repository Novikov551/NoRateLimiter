using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RateLimiter
{
    public sealed class DefaultRateLimitRejectionHandler : IRateLimitRejectionHandler
    {
        private readonly ILogger<DefaultRateLimitRejectionHandler> _logger;

        public DefaultRateLimitRejectionHandler(ILogger<DefaultRateLimitRejectionHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(HttpContext context)
        {
            _logger.LogWarning("Rate limit exceeded. Path: {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(new { message = "Rate limit exceeded." });

            await context.Response.WriteAsync(json);
        }
    }
}
