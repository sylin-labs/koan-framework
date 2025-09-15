using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Koan.Data.Abstractions;
using Koan.Orchestration;
using Koan.Orchestration.Attributes;
using StackExchange.Redis;

namespace Koan.Data.Redis;

[ProviderPriority(5)]
[KoanService(ServiceKind.Cache, shortCode: "redis", name: "Redis",
    ContainerImage = "redis",
    DefaultTag = "7",
    DefaultPorts = new[] { 6379 },
    Capabilities = new[] { "protocol=redis" },
    Volumes = new[] { "./Data/redis:/data" },
    AppEnv = new[] { "Koan__Data__Redis__Endpoint={scheme}://{host}:{port}" },
    Scheme = "redis", Host = "redis", EndpointPort = 6379, UriPattern = "redis://{host}:{port}",
    LocalScheme = "redis", LocalHost = "localhost", LocalPort = 6379, LocalPattern = "redis://{host}:{port}")]
public sealed class RedisAdapterFactory : IDataAdapterFactory
{
    public bool CanHandle(string provider) => string.Equals(provider, "redis", StringComparison.OrdinalIgnoreCase);
    public IDataRepository<TEntity, TKey> Create<TEntity, TKey>(IServiceProvider sp)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull
    {
        var opts = sp.GetRequiredService<IOptions<RedisOptions>>();
        var muxer = sp.GetRequiredService<IConnectionMultiplexer>();
        return new RedisRepository<TEntity, TKey>(opts, muxer, sp.GetService<ILoggerFactory>());
    }
}
