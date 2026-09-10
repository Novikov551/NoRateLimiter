using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RateLimiter.Extensions;
using RateLimiter.KeyProviders;
using RateLimiter.RateLimiters;
using RateLimiter.Storages;
using Xunit;

namespace RateLimiter.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static void AddDefaultPolicy(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
            options.AddTokenBucketLimiter("default", p =>
            {
                p.TokenLimit = 10;
                p.TokensPerPeriod = 2;
                p.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
            }));
    }

    [Fact]
    public void AddRateLimiter_WithoutPolicies_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddRateLimiter(_ => { }));
    }

    [Fact]
    public void AddRateLimiter_WithValidPolicy_RegistersAllCoreServices()
    {
        var services = CreateServices();

        AddDefaultPolicy(services);

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IRateLimitRejectionHandler>());
        Assert.NotNull(sp.GetService<IRateLimiterFactory>());
        Assert.NotNull(sp.GetService<IDataStorage>());
        Assert.NotNull(sp.GetService<IKeyProvider>());
    }

    [Fact]
    public void AddRateLimiter_RegistersInMemoryStorageAsDefault()
    {
        var services = CreateServices();

        AddDefaultPolicy(services);

        Assert.IsType<InMemoryDataStorage>(services.BuildServiceProvider().GetRequiredService<IDataStorage>());
    }

    [Fact]
    public void AddRateLimiter_RegistersIpKeyProviderAsDefault()
    {
        var services = CreateServices();

        AddDefaultPolicy(services);

        Assert.IsType<IpKeyProvider>(services.BuildServiceProvider().GetRequiredService<IKeyProvider>());
    }

    [Fact]
    public void AddRateLimiter_RegistersDefaultRejectionHandler()
    {
        var services = CreateServices();

        AddDefaultPolicy(services);

        Assert.IsType<DefaultRateLimitRejectionHandler>(
            services.BuildServiceProvider().GetRequiredService<IRateLimitRejectionHandler>());
    }

    [Fact]
    public void UseRejectionHandler_ReplacesDefaultHandler()
    {
        var services = CreateServices();
        AddDefaultPolicy(services);

        services.UseRejectionHandler<CustomRejectionHandler>();

        Assert.IsType<CustomRejectionHandler>(
            services.BuildServiceProvider().GetRequiredService<IRateLimitRejectionHandler>());
    }

    [Fact]
    public void UseStorage_ReplacesDefaultStorage()
    {
        var services = CreateServices();
        AddDefaultPolicy(services);

        services.UseStorage<CustomDataStorage>();

        Assert.IsType<CustomDataStorage>(
            services.BuildServiceProvider().GetRequiredService<IDataStorage>());
    }

    [Fact]
    public void UseKeyProvider_ReplacesDefaultKeyProvider()
    {
        var services = CreateServices();
        AddDefaultPolicy(services);

        services.UseKeyProvider<CustomKeyProvider>();

        Assert.IsType<CustomKeyProvider>(
            services.BuildServiceProvider().GetRequiredService<IKeyProvider>());
    }

    [Fact]
    public void UseAlgorithm_RegistersCustomAlgorithmFactory()
    {
        var services = CreateServices();

        services.UseAlgorithm<CustomAlgorithmFactory>();
        AddDefaultPolicy(services);

        var factories = services.BuildServiceProvider()
            .GetServices<IAlgorithmRateLimiterFactory>().ToList();

        Assert.Contains(factories, f => f is CustomAlgorithmFactory);
    }

    [Fact]
    public void AddRateLimiter_RegistersSingletonServices()
    {
        var services = CreateServices();
        AddDefaultPolicy(services);
        var sp = services.BuildServiceProvider();

        var storage1 = sp.GetRequiredService<IDataStorage>();
        var storage2 = sp.GetRequiredService<IDataStorage>();

        Assert.Same(storage1, storage2);
    }

    public class CustomRejectionHandler : IRateLimitRejectionHandler
    {
        public Task HandleAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            httpContext.Response.StatusCode = 429;
            return Task.CompletedTask;
        }
    }

    public class CustomDataStorage : IDataStorage
    {
        public ValueTask<int> GetLimitAsync(string key, CancellationToken ct = default)
            => ValueTask.FromResult(100);
        public ValueTask<int> GetRemainingAsync(string key, CancellationToken ct = default)
            => ValueTask.FromResult(100);
        public ValueTask<DateTime> GetResetAsync(string key, CancellationToken ct = default)
            => ValueTask.FromResult(DateTime.UtcNow);
        public ValueTask<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default)
            => ValueTask.FromResult(true);
    }

    public class CustomKeyProvider : IKeyProvider
    {
        public string GetKey(Microsoft.AspNetCore.Http.HttpContext context) => "custom-key";
    }

    public class CustomAlgorithmFactory : IAlgorithmRateLimiterFactory
    {
        public AlgorithmType Algorithm => new("Custom");
        public IRateLimiter Create(RateLimiterPolicy policy) => throw new NotImplementedException();
        public int GetDefaultRemaining(RateLimiterPolicy rateLimiterPolicy) => throw new NotImplementedException();
    }
}