using StackExchange.Redis;

namespace PortfolioChat.Services;

public class ValkeyService {
    private readonly IDatabase _db;

    public ValkeyService(IConnectionMultiplexer multiplexer, ConfigService config) {
        _db = multiplexer.GetDatabase(config.RedisDatabaseIndex);
    }
}

public static class ValkeyServiceExtensions {
    public static IServiceCollection AddValkeyService(this IServiceCollection services, ConfigService config) {
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(config.RedisConnectionString));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(config.RedisDatabaseIndex));
        services.AddSingleton(sp => new ValkeyService(
            sp.GetRequiredService<IConnectionMultiplexer>(), config));

        return services;
    }
}