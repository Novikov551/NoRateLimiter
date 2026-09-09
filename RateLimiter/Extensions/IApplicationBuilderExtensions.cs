using Microsoft.AspNetCore.Builder;

namespace RateLimiter.Extensions
{
    /// <summary>
    /// Регистрация rate limiter middleware в ASP.NET Core pipeline.
    /// Вызывать после <c>UseRouting()</c>, перед <c>MapControllers()</c>.
    /// </summary>
    public static class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Добавить <see cref="RateLimiterMiddleware"/> в pipeline.
        /// </summary>
        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RateLimiterMiddleware>();
        }
    }
}
