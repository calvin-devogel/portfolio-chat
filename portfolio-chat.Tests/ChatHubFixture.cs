using StackExchange.Redis;

namespace portfolio_chat.Tests;

public class ChatHubFixture : IAsyncLifetime
{
    public ChatWebApplicationFactory Factory { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await FlushTestDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        await FlushTestDatabase();
        await Factory.DisposeAsync();
    }

    private async Task FlushTestDatabase()
    {
        var db = Factory.Services.GetRequiredService<IDatabase>();
        await db.KeyDeleteAsync("chat:active_users");
        await db.KeyDeleteAsync("chat:messages");
    }
}