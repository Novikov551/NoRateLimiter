namespace RateLimiter.Storages
{
    public interface IDataStorage
    {
        ValueTask<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default);
        ValueTask<int> GetRemainingAsync(string key, CancellationToken ct = default);
        ValueTask<int> GetLimitAsync(string key, CancellationToken ct = default);
        ValueTask<DateTime> GetResetAsync(string key, CancellationToken ct = default);
    }
}
