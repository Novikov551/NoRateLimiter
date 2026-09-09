namespace RateLimiter.Attributes
{
    /// <summary>
    /// Назначает именованную политику rate limiting эндпоинту или контроллеру.
    /// Если не указан — используется политика "default".
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RateLimitPolicyAttribute : Attribute
    {
        /// <summary>
        /// Имя политики, зарегистрированной через <c>AddTokenBucketLimiter</c> и т.д.
        /// </summary>
        public string PolicyName { get;}

        public RateLimitPolicyAttribute(string policyName)
        {
            PolicyName = policyName;
        }
    }
}
