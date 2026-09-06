using Microsoft.AspNetCore.Http;

namespace RateLimiter.KeyProviders
{
    public class IpKeyProvider : IKeyProvider
    {
        public string GetKey(HttpContext context)
        {
            return GetClientIp(context) ?? "anonymus";
        }

        private string? GetClientIp(HttpContext context)
        {
            var forwared = context.Request.Headers["X-Forwared-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwared))
            {
                return forwared.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
