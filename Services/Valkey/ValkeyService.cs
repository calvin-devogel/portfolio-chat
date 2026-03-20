using StackExchange.Redis;

namespace PortfolioChat.Services;

public class ValkeyService
{
    private readonly IDatabase _db;

    public ValkeyService(IConnectionMultiplexer multiplexer, ConfigService config)
    {
        _db = multiplexer.GetDatabase(config.RedisDatabaseIndex);
    }
}

public static class ValkeyServiceExtensions
{
    public static IServiceCollection AddValkeyService(this IServiceCollection services, ConfigService config, ILogger<ValkeyService> logger)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(config.RedisConnectionString));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(config.RedisDatabaseIndex));
        services.AddSingleton(sp => new ValkeyService(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            config
        ));

        logger.LogInformation("ValkeyService registered with Redis at {RedisConnectionString} on database {RedisDatabaseIndex}",
            config.RedisConnectionString, config.RedisDatabaseIndex);

        return services;
    }
}