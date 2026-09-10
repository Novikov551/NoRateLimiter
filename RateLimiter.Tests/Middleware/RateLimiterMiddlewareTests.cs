using Microsoft.AspNetCore.Http;
using Moq;
using RateLimiter.Attributes;
using RateLimiter.KeyProviders;
using RateLimiter.RateLimiters;
using RateLimiter.Storages;
using Xunit;

namespace RateLimiter.Tests.Middleware;

public class RateLimiterMiddlewareTests
{
    private static Mock<IDataStorage> CreateStorageMock(bool consumeResult = true)
    {
        var mock = new Mock<IDataStorage>();
        mock.Setup(x => x.TryConsumeAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(consumeResult));
        mock.Setup(x => x.GetResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<DateTime>(DateTime.UtcNow.AddSeconds(60)));
        mock.Setup(x => x.GetLimitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(100));
        mock.Setup(x => x.GetRemainingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(consumeResult ? 99 : 0));
        return mock;
    }

    private static Mock<IKeyProvider> CreateKeyProviderMock(string key = "192.168.1.1")
    {
        var mock = new Mock<IKeyProvider>();
        mock.Setup(x => x.GetKey(It.IsAny<HttpContext>())).Returns(key);
        return mock;
    }

    private static DefaultHttpContext CreateContext(Endpoint? endpoint = null)
    {
        var context = new DefaultHttpContext();
        if (endpoint != null) context.SetEndpoint(endpoint);
        return context;
    }

    private static Endpoint CreateEndpoint(params object[] metadata)
    {
        return new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");
    }

    [Fact]
    public async Task InvokeAsync_WithSkipRateLimitingAttribute_SkipsStorageCompletely()
    {
        var storage = CreateStorageMock();
        var provider = CreateKeyProviderMock();
        var called = false;
        var middleware = new RateLimiterMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);

        await middleware.InvokeAsync(CreateContext(CreateEndpoint(new SkipRateLimitingAttribute())));

        Assert.True(called);
        storage.Verify(x => x.TryConsumeAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_SkipRateLimiting_DoesNotSetRateLimitHeaders()
    {
        var storage = CreateStorageMock();
        var provider = CreateKeyProviderMock();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);
        var context = CreateContext(CreateEndpoint(new SkipRateLimitingAttribute()));

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-RateLimit-Limit"));
    }

    [Fact]
    public async Task InvokeAsync_WhenConsumeAllowed_CallsNextDelegate()
    {
        var storage = CreateStorageMock(consumeResult: true);
        var provider = CreateKeyProviderMock();
        var called = false;
        var middleware = new RateLimiterMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);

        await middleware.InvokeAsync(CreateContext());

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_WhenConsumeAllowed_DoesNotCallRejectionHandler()
    {
        var storage = CreateStorageMock(consumeResult: true);
        var provider = CreateKeyProviderMock();
        var handler = new Mock<IRateLimitRejectionHandler>();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, handler.Object);

        await middleware.InvokeAsync(CreateContext());

        handler.Verify(x => x.HandleAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenConsumeAllowed_SetsAllRateLimitHeaders()
    {
        var storage = CreateStorageMock(consumeResult: true);
        var provider = CreateKeyProviderMock();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Limit"));
        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Remaining"));
        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Reset"));
        Assert.True(context.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task InvokeAsync_WhenConsumeDenied_DoesNotCallNextDelegate()
    {
        var storage = CreateStorageMock(consumeResult: false);
        var provider = CreateKeyProviderMock();
        var called = false;
        var middleware = new RateLimiterMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);

        await middleware.InvokeAsync(CreateContext());

        Assert.False(called);
    }

    [Fact]
    public async Task InvokeAsync_WhenConsumeDenied_CallsRejectionHandler()
    {
        var storage = CreateStorageMock(consumeResult: false);
        var provider = CreateKeyProviderMock();
        var handler = new Mock<IRateLimitRejectionHandler>();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, handler.Object);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        handler.Verify(x => x.HandleAsync(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenConsumeDenied_StillSetsRateLimitHeaders()
    {
        var storage = CreateStorageMock(consumeResult: false);
        var provider = CreateKeyProviderMock();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Limit"));
        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Remaining"));
        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Reset"));
        Assert.True(context.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task InvokeAsync_NoEndpoint_UsesDefaultPolicy()
    {
        var storage = CreateStorageMock();
        var provider = CreateKeyProviderMock();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);

        await middleware.InvokeAsync(CreateContext());

        storage.Verify(x =>
            x.TryConsumeAsync("default:192.168.1.1", 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithRateLimitPolicyAttribute_UsesPolicyFromAttribute()
    {
        var storage = CreateStorageMock();
        var provider = CreateKeyProviderMock();
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);
        var endpoint = CreateEndpoint(new RateLimitPolicyAttribute("strict"));

        await middleware.InvokeAsync(CreateContext(endpoint));

        storage.Verify(x =>
            x.TryConsumeAsync("strict:192.168.1.1", 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_UsesKeyFromProvider()
    {
        var storage = CreateStorageMock();
        var provider = CreateKeyProviderMock("10.0.0.1");
        var middleware = new RateLimiterMiddleware(
            _ => Task.CompletedTask,
            provider.Object, storage.Object, new Mock<IRateLimitRejectionHandler>().Object);

        await middleware.InvokeAsync(CreateContext());

        storage.Verify(x =>
            x.TryConsumeAsync("default:10.0.0.1", 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}