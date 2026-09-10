using RateLimiter.RateLimiters.TokenBucket;
using Xunit;

namespace RateLimiter.Tests.Algorithms;

public class TokenBucketTests
{
    [Theory]
    [InlineData(0, true, 5)]
    [InlineData(1, true, 4)]
    [InlineData(5, true, 0)]
    [InlineData(6, false, 5)]
    public void TryConsume_VariousAmounts_ReturnsExpectedAndRemaining(
        int amount, bool expected, int expectedRemaining)
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);

        var result = bucket.TryConsume(amount);

        Assert.Equal(expected, result);
        Assert.Equal(expectedRemaining, bucket.GetRemaining());
    }

    [Fact]
    public void TryConsume_WhenEmpty_ReturnsFalseAndKeepsZero()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 3);

        bucket.TryConsume(3);

        Assert.False(bucket.TryConsume(1));
        Assert.Equal(0, bucket.GetRemaining());
    }

    [Fact]
    public void TryConsume_MultipleSubtractableCalls_DecrementsCorrectly()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 10);

        bucket.TryConsume(3);
        bucket.TryConsume(2);
        var result = bucket.TryConsume(1);

        Assert.True(result);
        Assert.Equal(4, bucket.GetRemaining());
    }

    [Fact]
    public void GetRemaining_NewBucket_ReturnsCapacity()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 10);

        Assert.Equal(10, bucket.GetRemaining());
    }

    [Fact]
    public void GetRemaining_AfterPartialConsume_ReturnsDifference()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 10);

        bucket.TryConsume(7);

        Assert.Equal(3, bucket.GetRemaining());
    }

    [Fact]
    public void GetRemaining_AfterFullConsume_ReturnsZero()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);

        bucket.TryConsume(5);

        Assert.Equal(0, bucket.GetRemaining());
    }

    [Fact]
    public void GetReset_WhenHasTokens_ReturnsTimeNearNow()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);
        var before = DateTime.UtcNow;

        var reset = bucket.GetReset();

        Assert.InRange(reset, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void GetReset_WhenEmpty_ReturnsTimeAfterNow()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);

        bucket.TryConsume(5);
        var reset = bucket.GetReset();

        Assert.True(reset > DateTime.UtcNow);
    }

    [Fact]
    public void GetReset_WhenEmpty_ReturnsTimeProportionalToDeficit()
    {
        var bucket = new TokenBucket(refillRate: 2, capacity: 10);

        bucket.TryConsume(10);
        var reset = bucket.GetReset();

        var secondsUntilReset = (reset - DateTime.UtcNow).TotalSeconds;
        Assert.InRange(secondsUntilReset, 0.1, 5.5);
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_PreservesState()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 10);
        bucket.TryConsume(7);

        var json = bucket.Serialize();
        var restored = new TokenBucket(refillRate: 1, capacity: 10);
        restored.Deserialize(json);

        Assert.Equal(bucket.GetRemaining(), restored.GetRemaining());
    }

    [Fact]
    public void Serialize_Deserialize_EmptyBucket_RoundTrip_PreservesZero()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);
        bucket.TryConsume(5);

        var json = bucket.Serialize();
        var restored = new TokenBucket(refillRate: 1, capacity: 5);
        restored.Deserialize(json);

        Assert.Equal(0, restored.GetRemaining());
    }

    [Fact]
    public void Deserialize_WithMalformedJson_ThrowsJsonException()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);

        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            bucket.Deserialize("not valid json"));
    }


    [Fact]
    public void Serialize_ProducesDeserializableJson()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);
        bucket.TryConsume(2);

        var json = bucket.Serialize();
        var restored = new TokenBucket(refillRate: 1, capacity: 5);
        restored.Deserialize(json);

        Assert.Equal(3, restored.GetRemaining());
    }

    [Fact]
    public void TryConsume_AfterDeserialize_ContinuesFromRestoredState()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 10);
        bucket.TryConsume(8);

        var json = bucket.Serialize();
        var restored = new TokenBucket(refillRate: 1, capacity: 10);
        restored.Deserialize(json);

        Assert.True(restored.TryConsume(2));
        Assert.False(restored.TryConsume(1));
    }

    [Fact]
    public void TryConsume_AfterRefill_ConsumesNewlyRefilledTokens()
    {
        var bucket = new TokenBucket(refillRate: 10, capacity: 10);
        bucket.TryConsume(10);

        Thread.Sleep(1500);

        Assert.True(bucket.TryConsume(1));
    }

    [Fact]
    public void TryConsume_Refill_DoesNotExceedCapacity()
    {
        var bucket = new TokenBucket(refillRate: 1, capacity: 5);
        bucket.TryConsume(3);

        Thread.Sleep(10000);

        Assert.True(bucket.TryConsume(5));
        Assert.False(bucket.TryConsume(1));
    }
}