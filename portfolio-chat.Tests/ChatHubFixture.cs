using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace portfolio_chat.Tests;

public class ChatHubFixture : IAsyncLifetime {
    public ChatWebApplicationFactory Factory { get; } = new();

    public async ValueTask InitializeAsync() {
        await FlushTestDatabase();
    }

    public async ValueTask DisposeAsync() {
        await FlushTestDatabase();
        await Factory.DisposeAsync();
    }

    private async Task FlushTestDatabase() {
        var db = Factory.Services.GetRequiredService<IDatabase>();
        var connectionMultiplexer = Factory.Services.GetRequiredService<IConnectionMultiplexer>();
        foreach (var endpoint in connectionMultiplexer.GetEndPoints()) {
            var server = connectionMultiplexer.GetServer(endpoint);
            if (!server.IsReplica) {
                await server.FlushDatabaseAsync(db.Database);
            }
        }
    }
}