using RateLimiter.RateLimiters.SlidingWindow;
using Xunit;

namespace RateLimiter.Tests.Algorithms;

public class SlidingWindowTests
{
    [Theory]
    [InlineData(0, true, 3)]
    [InlineData(1, true, 2)]
    [InlineData(3, true, 0)]
    [InlineData(4, false, 3)]
    public void TryConsume_VariousAmounts_ReturnsExpectedAndRemaining(
        int amount, bool expected, int expectedRemaining)
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 3);

        var result = window.TryConsume(amount);

        Assert.Equal(expected, result);
        Assert.Equal(expectedRemaining, window.GetRemaining());
    }

    [Fact]
    public void TryConsume_AfterExhaustingLimit_ReturnsFalseAndKeepsZero()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 2);

        window.TryConsume(2);

        Assert.False(window.TryConsume(1));
        Assert.Equal(0, window.GetRemaining());
    }

    [Fact]
    public void TryConsume_AfterWindowExpires_AcceptsNewRequest()
    {
        var window = new SlidingWindow(TimeSpan.FromMilliseconds(500), maxRequests: 2);

        window.TryConsume(2);

        Thread.Sleep(600);

        Assert.True(window.TryConsume(1));
        Assert.Equal(1, window.GetRemaining());
    }

    [Fact]
    public void GetRemaining_NewWindow_ReturnsMaxRequests()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 10);

        Assert.Equal(10, window.GetRemaining());
    }

    [Fact]
    public void GetRemaining_AfterPartialConsume_ReturnsDifference()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 10);

        window.TryConsume(4);

        Assert.Equal(6, window.GetRemaining());
    }

    [Fact]
    public void GetRemaining_WhenExhausted_ReturnsZero()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 2);

        window.TryConsume(2);

        Assert.Equal(0, window.GetRemaining());
        Assert.True(window.GetRemaining() >= 0);
    }

    [Fact]
    public void GetReset_WhenUnderLimit_ReturnsTimeNearNow()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        var before = DateTime.UtcNow;

        var reset = window.GetReset();

        Assert.InRange(reset, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void GetReset_WhenAtLimit_ReturnsOldestRequestPlusWindow()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 2);

        window.TryConsume(2);
        var reset = window.GetReset();

        Assert.True(reset > DateTime.UtcNow);
    }

    [Fact]
    public void Serialize_ProducesDeserializableOutput()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        window.TryConsume(3);

        var json = window.Serialize();
        var restored = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        restored.Deserialize(json);

        Assert.Equal(2, restored.GetRemaining());
    }

    [Fact]
    public void Serialize_AfterWindowExpired_ExcludesExpiredRequests()
    {
        var window = new SlidingWindow(TimeSpan.FromMilliseconds(300), maxRequests: 5);
        window.TryConsume(3);

        Thread.Sleep(400);
        var json = window.Serialize();

        var restored = new SlidingWindow(TimeSpan.FromMilliseconds(300), maxRequests: 5);
        restored.Deserialize(json);

        Assert.Equal(5, restored.GetRemaining());
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_PreservesState()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        window.TryConsume(3);

        var json = window.Serialize();
        var restored = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        restored.Deserialize(json);

        Assert.Equal(window.GetRemaining(), restored.GetRemaining());
    }

    [Fact]
    public void Serialize_Deserialize_ContinuesFromRestoredState()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        window.TryConsume(4);

        var json = window.Serialize();
        var restored = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);
        restored.Deserialize(json);

        Assert.True(restored.TryConsume(1));
        Assert.False(restored.TryConsume(1));
    }

    [Fact]
    public void Deserialize_WithMalformedJson_ThrowsJsonException()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(10), maxRequests: 5);

        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            window.Deserialize("not valid json"));
    }

    [Fact]
    public void TryConsume_PartialWindowExpired_OnlyExpiredRequestsRemoved()
    {
        var window = new SlidingWindow(TimeSpan.FromMilliseconds(1000), maxRequests: 3);

        window.TryConsume(1);
        Thread.Sleep(600);
        window.TryConsume(1);
        Thread.Sleep(600);

        window.TryConsume(0);

        Assert.Equal(2, window.GetRemaining());
        Assert.True(window.TryConsume(2));
    }

    [Fact]
    public void TryConsume_ConcurrentCalls_AreThreadSafe()
    {
        var window = new SlidingWindow(TimeSpan.FromSeconds(5), maxRequests: 100);
        var successCount = 0;
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (window.TryConsume(1))
                    Interlocked.Increment(ref successCount);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.Equal(100, successCount);
        Assert.Equal(0, window.GetRemaining());
    }
}