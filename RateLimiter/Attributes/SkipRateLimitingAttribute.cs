namespace RateLimiter.Attributes
{
    /// <summary>
    /// Полностью пропускает rate limiting для эндпоинта. Middleware не проверяет
    /// лимит и не устанавливает заголовки X-RateLimit-*.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class SkipRateLimitingAttribute : Attribute
    {
    }
}
