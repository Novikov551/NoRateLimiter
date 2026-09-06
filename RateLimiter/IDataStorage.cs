namespace RateLimiter
{
    public interface IDataStorage
    {
        Task<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default);
        Task<int> GetRemainingAsync(string key, CancellationToken ct = default);
        int GetLimit(string key);
        Task<DateTime> GetResetAsync(string key, CancellationToken ct = default);
    }
}
