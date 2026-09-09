namespace RateLimiter.RateLimiters
{
    public interface ISerializableRateLimiter : IRateLimiter
    {
        string Serialize();
        void Deserialize(string state);
    }
}
