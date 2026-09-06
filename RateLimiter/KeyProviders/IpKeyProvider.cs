using Microsoft.AspNetCore.Http;

namespace RateLimiter.KeyProviders
{
    public class IpKeyProvider : IKeyProvider
    {
        public string GetKey(HttpContext context)
        {
            return GetClientIp(context) ?? "anonymous";
        }

        private string? GetClientIp(HttpContext context)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
