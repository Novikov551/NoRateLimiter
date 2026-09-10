using Microsoft.AspNetCore.Http;
using RateLimiter.KeyProviders;
using Xunit;

namespace RateLimiter.Tests.KeyProviders;

public class IpKeyProviderTests
{
    [Fact]
    public void GetKey_WithRemoteIp_ReturnsIpAddressString()
    {
        var provider = new IpKeyProvider();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        var key = provider.GetKey(context);

        Assert.Equal("192.168.1.100", key);
    }

    [Fact]
    public void GetKey_WithDifferentIps_ReturnsDifferentKeys()
    {
        var provider = new IpKeyProvider();
        var ctx1 = new DefaultHttpContext();
        ctx1.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
        var ctx2 = new DefaultHttpContext();
        ctx2.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.2");

        Assert.NotEqual(provider.GetKey(ctx1), provider.GetKey(ctx2));
    }

    [Fact]
    public void GetKey_WhenNoRemoteIpAndNoForwardedHeader_ReturnsAnonymous()
    {
        var provider = new IpKeyProvider();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;

        var key = provider.GetKey(context);

        Assert.Equal("anonymous", key);
    }

    [Fact]
    public void GetKey_WhenXForwardedForPresent_PrefersFirstAddress()
    {
        var provider = new IpKeyProvider();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.5, 10.0.0.6";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        var key = provider.GetKey(context);

        Assert.Equal("10.0.0.5", key);
    }

    [Fact]
    public void GetKey_WhenXForwardedForSingleAddress_ReturnsThatAddress()
    {
        var provider = new IpKeyProvider();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "172.16.0.1";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");

        var key = provider.GetKey(context);

        Assert.Equal("172.16.0.1", key);
    }

    [Fact]
    public void GetKey_WhenXForwardedForEmpty_FallsBackToRemoteIp()
    {
        var provider = new IpKeyProvider();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");

        var key = provider.GetKey(context);

        Assert.Equal("10.0.0.1", key);
    }
}