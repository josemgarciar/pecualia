using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Pecualia.Api.Infrastructure.Security;

namespace Pecualia.Test.Infrastructure;

public sealed class ProxyConfigurationTests
{
    [Fact]
    public void DirectHosting_PreservesTrustedLoopbackDefaults()
    {
        var options = Configure();

        options.KnownProxies.Should().Contain(IPAddress.IPv6Loopback);
        options.KnownNetworks.Should().NotBeEmpty();
        options.ForwardLimit.Should().Be(1);
    }

    [Fact]
    public void Render_PreservesManagedIngressCompatibility()
    {
        var options = Configure(("RENDER", "true"));

        options.KnownProxies.Should().BeEmpty();
        options.KnownNetworks.Should().BeEmpty();
    }

    [Fact]
    public void Render_ExplicitConfigurationOverridesTrustAll()
    {
        var options = Configure(("RENDER", "true"), ("ForwardedHeaders:KnownProxies:0", "10.0.0.2"));

        options.KnownProxies.Should().Equal(IPAddress.Parse("10.0.0.2"));
        options.KnownNetworks.Should().BeEmpty();
    }

    [Fact]
    public void Render_CanDisableTrustAll()
    {
        var options = Configure(("RENDER", "true"), ("ForwardedHeaders:TrustAllProxies", "false"));

        options.KnownProxies.Should().NotBeEmpty();
        options.KnownNetworks.Should().NotBeEmpty();
    }

    private static ForwardedHeadersOptions Configure(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
        var options = new ForwardedHeadersOptions();
        ProxyConfiguration.Configure(options, configuration);
        return options;
    }
}
