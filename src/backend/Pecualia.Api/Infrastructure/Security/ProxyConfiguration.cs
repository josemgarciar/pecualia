using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Pecualia.Api.Infrastructure.Security;

public static class ProxyConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var isRender = string.Equals(configuration["RENDER"], "true", StringComparison.OrdinalIgnoreCase);
        var trustAll = configuration.GetValue<bool?>("ForwardedHeaders:TrustAllProxies")
                       ?? (isRender && proxies.Length == 0);

        // Render's managed ingress remains compatible; direct hosting trusts only configured proxies.
        if (trustAll || proxies.Length > 0)
        {
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        }

        if (!trustAll)
        {
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        }
    }
}
