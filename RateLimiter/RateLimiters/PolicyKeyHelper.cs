namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Формирует ключи для хранилища в формате <c>{policyName}:{clientKey}</c>
    /// и извлекает имя политики из ключа.
    /// </summary>
    public static class PolicyKeyHelper
    {
        private const char Separator = ':';

        public static string BuildKey(string policyName,
            string clientKey)
        {
            return $"{policyName}{Separator}{clientKey}";
        }

        public static string GetPolicyName(string key)
        {
            var splittedKey = key.Split(Separator);
            return splittedKey[0].Length > 0? splittedKey[0] : "default";
        }
    }
}
