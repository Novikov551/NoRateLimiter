namespace RateLimiter
{
    public class UserLastAccessDto
    {
        public UserLastAccessDto(IRateLimiter limiter, DateTime lastAccess)
        {
            Limiter = limiter;
            LastAccess = lastAccess;
        }

        public DateTime LastAccess { get; set; }
        public IRateLimiter Limiter { get; init; }
    }
}
