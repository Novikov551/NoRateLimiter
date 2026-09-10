using RateLimiter.Extensions;
using RateLimiter.RateLimiters;
using RateLimiter.RateLimiters.SlidingWindow;
using RateLimiter.RateLimiters.TokenBucket;
using Xunit;

namespace RateLimiter.Tests.Factories;

public class TokenBucketRateLimiterFactoryTests
{
    [Fact]
    public void Algorithm_ReturnsTokenBucketType()
    {
        var factory = new TokenBucketRateLimiterFactory();

        Assert.Equal(AlgorithmType.TokenBucket, factory.Algorithm);
    }

    [Fact]
    public void Create_WithValidPolicy_ReturnsTokenBucketInstance()
    {
        var factory = new TokenBucketRateLimiterFactory();
        var policy = new TokenBucketRateLimiterPolicy
        {
            TokenLimit = 10,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        var limiter = factory.Create(policy);

        Assert.IsType<TokenBucket>(limiter);
        Assert.Equal(10, limiter.GetRemaining());
    }

    [Fact]
    public void Create_EachCall_ReturnsNewInstance()
    {
        var factory = new TokenBucketRateLimiterFactory();
        var policy = new TokenBucketRateLimiterPolicy
        {
            TokenLimit = 10,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        Assert.NotSame(factory.Create(policy), factory.Create(policy));
    }

    [Fact]
    public void Create_WithWrongPolicyType_ThrowsInvalidCastException()
    {
        var factory = new TokenBucketRateLimiterFactory();

        Assert.Throws<InvalidCastException>(() =>
            factory.Create(new SlidingWindowRateLimiterPolicy()));
    }

    [Fact]
    public void GetDefaultRemaining_ReturnsTokenLimit()
    {
        var factory = new TokenBucketRateLimiterFactory();
        var policy = new TokenBucketRateLimiterPolicy
        {
            TokenLimit = 42,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        Assert.Equal(42, factory.GetDefaultRemaining(policy));
    }

    [Fact]
    public void GetDefaultRemaining_WithWrongPolicyType_ThrowsInvalidCastException()
    {
        var factory = new TokenBucketRateLimiterFactory();

        Assert.Throws<InvalidCastException>(() =>
            factory.GetDefaultRemaining(new SlidingWindowRateLimiterPolicy()));
    }
}

public class SlidingWindowRateLimiterFactoryTests
{
    [Fact]
    public void Algorithm_ReturnsSlidingWindowType()
    {
        var factory = new SlidingWindowRateLimiterFactory();

        Assert.Equal(AlgorithmType.SlidingWindow, factory.Algorithm);
    }

    [Fact]
    public void Create_WithValidPolicy_ReturnsSlidingWindowInstance()
    {
        var factory = new SlidingWindowRateLimiterFactory();
        var policy = new SlidingWindowRateLimiterPolicy
        {
            RequestsLimit = 10,
            Window = TimeSpan.FromSeconds(10)
        };

        var limiter = factory.Create(policy);

        Assert.IsType<SlidingWindow>(limiter);
        Assert.Equal(10, limiter.GetRemaining());
    }

    [Fact]
    public void Create_EachCall_ReturnsNewInstance()
    {
        var factory = new SlidingWindowRateLimiterFactory();
        var policy = new SlidingWindowRateLimiterPolicy
        {
            RequestsLimit = 10,
            Window = TimeSpan.FromSeconds(10)
        };

        Assert.NotSame(factory.Create(policy), factory.Create(policy));
    }

    [Fact]
    public void Create_WithWrongPolicyType_ThrowsInvalidCastException()
    {
        var factory = new SlidingWindowRateLimiterFactory();

        Assert.Throws<InvalidCastException>(() =>
            factory.Create(new TokenBucketRateLimiterPolicy()));
    }

    [Fact]
    public void GetDefaultRemaining_ReturnsRequestsLimit()
    {
        var factory = new SlidingWindowRateLimiterFactory();
        var policy = new SlidingWindowRateLimiterPolicy
        {
            RequestsLimit = 100,
            Window = TimeSpan.FromSeconds(10)
        };

        Assert.Equal(100, factory.GetDefaultRemaining(policy));
    }

    [Fact]
    public void GetDefaultRemaining_WithWrongPolicyType_ThrowsInvalidCastException()
    {
        var factory = new SlidingWindowRateLimiterFactory();

        Assert.Throws<InvalidCastException>(() =>
            factory.GetDefaultRemaining(new TokenBucketRateLimiterPolicy()));
    }
}

public class CompositeRateLimiterFactoryTests
{
    private static CompositeRateLimiterFactory CreateFactory(
        Action<RateLimiterOptions> configure)
    {
        var options = new RateLimiterOptions();
        configure(options);

        var factories = new List<IAlgorithmRateLimiterFactory>
        {
            new TokenBucketRateLimiterFactory(),
            new SlidingWindowRateLimiterFactory()
        };

        return new CompositeRateLimiterFactory(factories, options);
    }

    [Fact]
    public void Create_WithTokenBucketPolicy_ReturnsTokenBucket()
    {
        var factory = CreateFactory(o =>
            o.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 2;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            }));

        var limiter = factory.Create("api");

        Assert.IsType<TokenBucket>(limiter);
        Assert.Equal(10, limiter.GetRemaining());
    }

    [Fact]
    public void Create_WithSlidingWindowPolicy_ReturnsSlidingWindow()
    {
        var factory = CreateFactory(o =>
            o.AddSlidingWindowLimiter("web", p =>
            {
                p.RequestsLimit = 20;
                p.Window = TimeSpan.FromSeconds(30);
            }));

        var limiter = factory.Create("web");

        Assert.IsType<SlidingWindow>(limiter);
        Assert.Equal(20, limiter.GetRemaining());
    }

    [Fact]
    public void Create_WithUnknownPolicyName_ThrowsInvalidOperationException()
    {
        var factory = CreateFactory(o =>
            o.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 1;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            }));

        Assert.Throws<InvalidOperationException>(() => factory.Create("nonexistent"));
    }

    [Fact]
    public void Create_MultipleCallsForSamePolicy_ReturnsNewInstanceEachTime()
    {
        var factory = CreateFactory(o =>
            o.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 1;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            }));

        Assert.NotSame(factory.Create("api"), factory.Create("api"));
    }

    [Fact]
    public void GetDefaultRemaining_WithTokenBucket_ReturnsTokenLimit()
    {
        var factory = CreateFactory(o =>
            o.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 50;
                p.TokensPerPeriod = 5;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            }));

        Assert.Equal(50, factory.GetDefaultRemaining("api"));
    }

    [Fact]
    public void GetDefaultRemaining_WithSlidingWindow_ReturnsRequestsLimit()
    {
        var factory = CreateFactory(o =>
            o.AddSlidingWindowLimiter("web", p =>
            {
                p.RequestsLimit = 100;
                p.Window = TimeSpan.FromSeconds(60);
            }));

        Assert.Equal(100, factory.GetDefaultRemaining("web"));
    }

    [Fact]
    public void GetDefaultRemaining_WithUnknownPolicy_ThrowsInvalidOperationException()
    {
        var factory = CreateFactory(o =>
            o.AddTokenBucketLimiter("api", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 1;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            }));

        Assert.Throws<InvalidOperationException>(() =>
            factory.GetDefaultRemaining("nonexistent"));
    }

    [Fact]
    public void Constructor_WithoutFactoryForPolicyAlgorithm_ThrowsInvalidOperationException()
    {
        var options = new RateLimiterOptions();
        options.AddTokenBucketLimiter("api", p =>
        {
            p.TokenLimit = 10;
            p.TokensPerPeriod = 1;
            p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });

        Assert.Throws<InvalidOperationException>(() =>
            new CompositeRateLimiterFactory(Array.Empty<IAlgorithmRateLimiterFactory>(), options));
    }

    [Fact]
    public void Constructor_WithNullFactories_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompositeRateLimiterFactory(null!, new RateLimiterOptions()));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        var factories = new List<IAlgorithmRateLimiterFactory>
        {
            new TokenBucketRateLimiterFactory()
        };

        Assert.Throws<ArgumentNullException>(() =>
            new CompositeRateLimiterFactory(factories, null!));
    }
}