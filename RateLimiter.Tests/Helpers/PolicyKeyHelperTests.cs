using RateLimiter.RateLimiters;
using Xunit;

namespace RateLimiter.Tests.Helpers;

public class PolicyKeyHelperTests
{
    [Theory]
    [InlineData("default", "192.168.1.1", "default:192.168.1.1")]
    [InlineData("strict", "user123", "strict:user123")]
    [InlineData("a", "b", "a:b")]
    public void BuildKey_ReturnsFormattedKey(string policy, string clientKey, string expected)
    {
        Assert.Equal(expected, PolicyKeyHelper.BuildKey(policy, clientKey));
    }

    [Theory]
    [InlineData("default:192.168.1.1", "default")]
    [InlineData("strict:user123", "strict")]
    public void GetPolicyName_ExtractsPolicyBeforeColon(string key, string expected)
    {
        Assert.Equal(expected, PolicyKeyHelper.GetPolicyName(key));
    }

    [Fact]
    public void GetPolicyName_WhenEmptyBeforeColon_ReturnsDefault()
    {
        Assert.Equal("default", PolicyKeyHelper.GetPolicyName(":somekey"));
    }

    [Fact]
    public void GetPolicyName_WhenNoColonPresent_ReturnsWholeString()
    {
        Assert.Equal("nopolicy", PolicyKeyHelper.GetPolicyName("nopolicy"));
    }

    [Fact]
    public void BuildKey_RoundTrip_PreservesPolicyName()
    {
        var key = PolicyKeyHelper.BuildKey("myPolicy", "10.0.0.1");

        Assert.Equal("myPolicy", PolicyKeyHelper.GetPolicyName(key));
    }
}