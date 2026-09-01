namespace RateLimiter
{
    public interface IRateLimiter
    {
        bool TryConsume(int tokens = 1);
        int GetRemaining();
        int GetLimit();
        DateTime GetReset();
    }
}
