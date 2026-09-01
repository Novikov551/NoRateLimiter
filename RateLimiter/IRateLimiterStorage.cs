namespace RateLimiter
{
    public interface IRateLimiterStorage
    {
        bool TryConsume(string key, int tokens = 1);
        int GetRemaining(string key);
        int GetLimit(string key);
        DateTime GetReset(string key);
    }
}
