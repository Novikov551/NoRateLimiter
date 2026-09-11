using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RateLimiter
{
    /// <summary>
    /// Дефолтный обработчик 429. Логирует превышение лимита и возвращает
    /// JSON: <c>{"message":"Rate limit exceeded."}</c>.
    /// </summary>
    public sealed class DefaultRateLimitRejectionHandler : IRateLimitRejectionHandler
    {
        private readonly ILogger<DefaultRateLimitRejectionHandler> _logger;
        private readonly static string jsonResponse = JsonSerializer.Serialize(new { message = "Rate limit exceeded." });
       
        public DefaultRateLimitRejectionHandler(ILogger<DefaultRateLimitRejectionHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(HttpContext context)
        {
            _logger.LogWarning("Rate limit exceeded. Path: {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
