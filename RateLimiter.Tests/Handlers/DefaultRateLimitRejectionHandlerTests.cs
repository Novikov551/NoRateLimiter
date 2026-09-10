using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xunit;

namespace RateLimiter.Tests.Handlers;

public class DefaultRateLimitRejectionHandlerTests
{
    private static DefaultRateLimitRejectionHandler CreateHandler()
    {
        var logger = LoggerFactory.Create(b => { }).CreateLogger<DefaultRateLimitRejectionHandler>();
        return new DefaultRateLimitRejectionHandler(logger);
    }

    [Fact]
    public async Task HandleAsync_SetsStatusCode429()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.HandleAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SetsContentTypeApplicationJson()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.HandleAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task HandleAsync_WritesBodyWithRateLimitExceededMessage()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.HandleAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("message", out var message));
        Assert.Equal("Rate limit exceeded.", message.GetString());
    }

    [Fact]
    public async Task HandleAsync_WritesNonEmptyResponseBody()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.HandleAsync(context);

        Assert.True(context.Response.Body.Length > 0);
    }

    [Fact]
    public async Task HandleAsync_DoesNotTouchExistingResponseHeaders()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Headers["X-Custom-Header"] = "preserved";
        context.Response.Body = new MemoryStream();

        await handler.HandleAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("preserved", context.Response.Headers["X-Custom-Header"]);
    }
}