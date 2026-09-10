using Microsoft.Extensions.Caching.Memory;
using Moq;
using RateLimiter.RateLimiters;
using RateLimiter.Storages;
using Xunit;

namespace RateLimiter.Tests.Storages;

public class InMemoryDataStorageTests
{
    private static readonly MemoryCacheOptions CacheOptions = new() { SizeLimit = 10_000 };
    private static readonly MemoryCacheEntryOptions EntryOptions = new()
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromMinutes(1)
    };

    private static InMemoryDataStorage CreateStorageWithFactory(
        Mock<IRateLimiterFactory> factory)
    {
        return new InMemoryDataStorage(factory.Object, CacheOptions, EntryOptions);
    }

    private static Mock<IRateLimiterFactory> CreateFactoryReturning(
        Mock<IRateLimiter> limiter, string policyName = "default", int defaultRemaining = 10)
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create(policyName)).Returns(limiter.Object);
        factory.Setup(x => x.GetDefaultRemaining(policyName)).Returns(defaultRemaining);
        return factory;
    }

    private static Mock<IRateLimiter> CreateLimiter(
        bool tryConsumeResult = true, int remaining = 10, int? consumeTimes = null)
    {
        var limiter = new Mock<IRateLimiter>();
        var setup = limiter.Setup(x => x.TryConsume(1)).Returns(tryConsumeResult);
        if (consumeTimes.HasValue)
            setup.Verifiable();
        limiter.Setup(x => x.GetRemaining()).Returns(remaining);
        limiter.Setup(x => x.GetReset()).Returns(DateTime.UtcNow.AddSeconds(60));
        return limiter;
    }

    [Fact]
    public async Task TryConsumeAsync_WhenLimiterReturnsTrue_ReturnsTrue()
    {
        var limiter = CreateLimiter(tryConsumeResult: true);
        var factory = CreateFactoryReturning(limiter);
        var storage = CreateStorageWithFactory(factory);

        var result = await storage.TryConsumeAsync("default:user1");

        Assert.True(result);
    }

    [Fact]
    public async Task TryConsumeAsync_WhenLimiterReturnsFalse_ReturnsFalse()
    {
        var limiter = CreateLimiter(tryConsumeResult: false, remaining: 0);
        var factory = CreateFactoryReturning(limiter);
        var storage = CreateStorageWithFactory(factory);

        var result = await storage.TryConsumeAsync("default:user1");

        Assert.False(result);
    }

    [Fact]
    public async Task TryConsumeAsync_SameKeyMultipleTimes_ReusesExistingLimiter()
    {
        var limiter = CreateLimiter();
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create("default")).Returns(limiter.Object);
        factory.Setup(x => x.GetDefaultRemaining("default")).Returns(10);
        var storage = CreateStorageWithFactory(factory);

        await storage.TryConsumeAsync("default:user1");
        await storage.TryConsumeAsync("default:user1");

        factory.Verify(x => x.Create("default"), Times.Once);
        limiter.Verify(x => x.TryConsume(1), Times.Exactly(2));
    }

    [Fact]
    public async Task TryConsumeAsync_DifferentKeys_CreatesSeparateLimiters()
    {
        var limiter1 = CreateLimiter();
        var limiter2 = CreateLimiter();

        var factory = new Mock<IRateLimiterFactory>();
        factory.SetupSequence(x => x.Create("default"))
               .Returns(limiter1.Object)
               .Returns(limiter2.Object);
        factory.Setup(x => x.GetDefaultRemaining("default")).Returns(10);
        var storage = CreateStorageWithFactory(factory);

        await storage.TryConsumeAsync("default:user1");
        await storage.TryConsumeAsync("default:user2");

        factory.Verify(x => x.Create("default"), Times.Exactly(2));
    }

    [Fact]
    public async Task GetRemainingAsync_WithExistingLimiter_ReturnsLimiterRemaining()
    {
        var limiter = CreateLimiter(remaining: 7);
        var factory = CreateFactoryReturning(limiter);
        var storage = CreateStorageWithFactory(factory);

        await storage.TryConsumeAsync("default:user1");
        var remaining = await storage.GetRemainingAsync("default:user1");

        Assert.Equal(7, remaining);
    }

    [Fact]
    public async Task GetRemainingAsync_UnknownKey_ReturnsFactoryDefault()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.GetDefaultRemaining("default")).Returns(42);
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);

        var remaining = await storage.GetRemainingAsync("default:unknown");

        Assert.Equal(42, remaining);
    }

    [Fact]
    public async Task GetLimitAsync_ReturnsFactoryDefaultRegardlessOfKey()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.GetDefaultRemaining("default")).Returns(100);
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);

        var limit = await storage.GetLimitAsync("default:anykey");

        Assert.Equal(100, limit);
    }

    [Fact]
    public async Task GetResetAsync_WithExistingLimiter_ReturnsLimiterResetTime()
    {
        var expectedReset = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var limiter = CreateLimiter();
        limiter.Setup(x => x.GetReset()).Returns(expectedReset);
        var factory = CreateFactoryReturning(limiter);
        var storage = CreateStorageWithFactory(factory);

        await storage.TryConsumeAsync("default:user1");
        var reset = await storage.GetResetAsync("default:user1");

        Assert.Equal(expectedReset, reset);
    }

    [Fact]
    public async Task GetResetAsync_UnknownKey_ReturnsApproximatelyUtcNow()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);
        var before = DateTime.UtcNow;

        var reset = await storage.GetResetAsync("default:unknown");

        Assert.InRange(reset, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task TryConsumeAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => storage.TryConsumeAsync("default:user1", ct: cts.Token).AsTask());
    }

    [Fact]
    public async Task GetRemainingAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => storage.GetRemainingAsync("default:user1", ct: cts.Token).AsTask());
    }

    [Fact]
    public async Task GetLimitAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => storage.GetLimitAsync("default:user1", ct: cts.Token).AsTask());
    }

    [Fact]
    public async Task GetResetAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(new Mock<IRateLimiter>().Object);
        var storage = CreateStorageWithFactory(factory);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => storage.GetResetAsync("default:user1", ct: cts.Token).AsTask());
    }

    [Fact]
    public async Task TryConsumeAsync_ExtractsPolicyNameFromCompoundKey()
    {
        var limiter = CreateLimiter();
        var factory = new Mock<IRateLimiterFactory>();
        factory.Setup(x => x.Create("strict")).Returns(limiter.Object);
        factory.Setup(x => x.GetDefaultRemaining("strict")).Returns(10);
        var storage = CreateStorageWithFactory(factory);

        await storage.TryConsumeAsync("strict:user1");

        factory.Verify(x => x.Create("strict"), Times.Once);
    }
}