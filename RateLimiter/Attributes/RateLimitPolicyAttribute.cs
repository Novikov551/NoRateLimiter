namespace RateLimiter.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RateLimitPolicyAttribute : Attribute
    {
        public string PolicyName { get;}

        public RateLimitPolicyAttribute(string policyName)
        {
            PolicyName = policyName;
        }
    }
}
