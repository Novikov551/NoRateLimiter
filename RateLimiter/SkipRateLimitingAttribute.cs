namespace RateLimiter
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class SkipRateLimitingAttribute : Attribute
    {
    }
}
