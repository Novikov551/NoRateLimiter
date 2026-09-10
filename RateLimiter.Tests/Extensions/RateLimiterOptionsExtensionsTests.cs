using RateLimiter.Extensions;
using RateLimiter.RateLimiters;
using RateLimiter.RateLimiters.SlidingWindow;
using RateLimiter.RateLimiters.TokenBucket;
using Xunit;

namespace RateLimiter.Tests.Extensions;

public class RateLimiterOptionsExtensionsTests
{
    [Fact]
    public void AddTokenBucketLimiter_WithValidParameters_RegistersPolicyWithCorrectValues()
    {
        var options = new RateLimiterOptions();

        options.AddTokenBucketLimiter("api", p =>
        {
            p.TokenLimit = 10;
            p.TokensPerPeriod = 2;
            p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });

        Assert.Single(options.Policies);
        var policy = (TokenBucketRateLimiterPolicy)options.Policies["api"];
        Assert.Equal("api", policy.Name);
        Assert.Equal(10, policy.TokenLimit);
        Assert.Equal(2, policy.TokensPerPeriod);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.ReplenishmentPeriod);
        Assert.Equal(AlgorithmType.TokenBucket, policy.Algorithm);
    }

    [Fact]
    public void AddTokenBucketLimiter_ReturnsOptionsForChaining()
    {
        var options = new RateLimiterOptions();

        var result = options.AddTokenBucketLimiter("api", p =>
        {
            p.TokenLimit = 10;
            p.TokensPerPeriod = 1;
            p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });

        Assert.Same(options, result);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(10, -1, 1)]
    public void AddTokenBucketLimiter_WithInvalidTokenOrPeriodParameters_ThrowsArgumentException(
        int tokenLimit, int tokensPerPeriod, int periodSeconds)
    {
        var options = new RateLimiterOptions();

        Assert.Throws<ArgumentException>(() =>
            options.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = tokenLimit;
                p.TokensPerPeriod = tokensPerPeriod;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(periodSeconds);
            }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddTokenBucketLimiter_WithInvalidReplenishmentPeriod_ThrowsArgumentException(
        int periodSeconds)
    {
        var options = new RateLimiterOptions();

        Assert.Throws<ArgumentException>(() =>
            options.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 1;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(periodSeconds);
            }));
    }

    [Fact]
    public void AddSlidingWindowLimiter_WithValidParameters_RegistersPolicyWithCorrectValues()
    {
        var options = new RateLimiterOptions();

        options.AddSlidingWindowLimiter("web", p =>
        {
            p.RequestsLimit = 20;
            p.Window = TimeSpan.FromSeconds(30);
        });

        Assert.Single(options.Policies);
        var policy = (SlidingWindowRateLimiterPolicy)options.Policies["web"];
        Assert.Equal("web", policy.Name);
        Assert.Equal(20, policy.RequestsLimit);
        Assert.Equal(TimeSpan.FromSeconds(30), policy.Window);
        Assert.Equal(AlgorithmType.SlidingWindow, policy.Algorithm);
    }

    [Fact]
    public void AddSlidingWindowLimiter_ReturnsOptionsForChaining()
    {
        var options = new RateLimiterOptions();

        var result = options.AddSlidingWindowLimiter("web", p =>
        {
            p.RequestsLimit = 10;
            p.Window = TimeSpan.FromSeconds(10);
        });

        Assert.Same(options, result);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    public void AddSlidingWindowLimiter_WithInvalidParameters_ThrowsArgumentException(
        int requestsLimit, int windowSeconds)
    {
        var options = new RateLimiterOptions();

        Assert.Throws<ArgumentException>(() =>
            options.AddSlidingWindowLimiter("web", p =>
            {
                p.RequestsLimit = requestsLimit;
                p.Window = TimeSpan.FromSeconds(windowSeconds);
            }));
    }

    [Fact]
    public void ChainingTokenBucketAndSlidingWindow_RegistersBothPolicies()
    {
        var options = new RateLimiterOptions();

        options
            .AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 2;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            })
            .AddSlidingWindowLimiter("web", p =>
            {
                p.RequestsLimit = 50;
                p.Window = TimeSpan.FromMinutes(1);
            });

        Assert.Equal(2, options.Policies.Count);
        Assert.True(options.Policies.ContainsKey("api"));
        Assert.True(options.Policies.ContainsKey("web"));
    }

    [Fact]
    public void AddSamePolicyNameTwice_SecondRegistrationOverwritesFirst()
    {
        var options = new RateLimiterOptions();

        options.AddTokenBucketLimiter("api", p =>
        {
            p.TokenLimit = 10;
            p.TokensPerPeriod = 1;
            p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });
        options.AddTokenBucketLimiter("api", p =>
        {
            p.TokenLimit = 50;
            p.TokensPerPeriod = 5;
            p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });

        Assert.Single(options.Policies);
        Assert.Equal(50, ((TokenBucketRateLimiterPolicy)options.Policies["api"]).TokenLimit);
    }
}