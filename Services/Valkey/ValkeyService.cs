using StackExchange.Redis;

namespace PortfolioChat.Services;

public static class ValkeyServiceExtensions {
    public static IServiceCollection AddValkeyService(this IServiceCollection services, ConfigService config) {
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(config.RedisConnectionString));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(config.RedisDatabaseIndex));

        return services;
    }
}