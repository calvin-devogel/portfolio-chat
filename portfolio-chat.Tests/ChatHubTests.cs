using Microsoft.AspNetCore.SignalR.Client;
using StackExchange.Redis;

namespace portfolio_chat.Tests;

public class ChatHubTests : IClassFixture<ChatHubFixture>, IAsyncLifetime
{
    private readonly ChatHubFixture _fixture;
    private readonly IDatabase _db;

    public ChatHubTests(ChatHubFixture fixture)
    {
        _fixture = fixture;
        _db = fixture.Factory.Services.GetRequiredService<IDatabase>();
    }

    public async ValueTask InitializeAsync()
    {
        await _db.KeyDeleteAsync("chat:active_users");
        await _db.KeyDeleteAsync("chat:messages");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private HubConnection CreateHubConnection(string userId = "user-1", string userName = "Charlie Chatter")
    {
        var server = _fixture.Factory.Server;

        TestAuthHandler.TestUserId = userId;
        TestAuthHandler.TestUserName = userName;

        return new HubConnectionBuilder()
            .withUrl(
                $"{server.BaseAddress}chathub",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult("test-token");
                })
            .Build();
    }

    [Fact]
    public async Task Connect_AddsUserToActiveUsers()
    {
        var connection = CreateHubConnection("user-1", "Alice");

        await connection.StartAsync();

        var name = await _db.HashGetAsync("chat:active_users", "user-1");
        Assert.Equal("Alice", name.ToString());

        await connection.StopAsync();
    }
}