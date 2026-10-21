using StackExchange.Redis;
using Serilog;

namespace PortfolioChat.Services;

public static class ValkeyServiceExtensions {
    public static async Task<bool> TryAddValkeyService(
        this IServiceCollection services,
        ConfigService config
    ) {
        try {
            var connection = await ConnectionMultiplexer.ConnectAsync(config.RedisConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(_ => connection);
            services.AddSingleton<IDatabase>(sp =>
                sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(config.RedisDatabaseIndex));

            return true;
        }
        catch (RedisException exception) {
            Log.Warning(exception, "Valkey is unavailable; chat features will be disabled");
            return false;
        }
    }
}