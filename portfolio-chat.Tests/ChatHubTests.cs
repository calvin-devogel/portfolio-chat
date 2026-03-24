using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace portfolio_chat.Tests;

public class ChatHubTests : IClassFixture<ChatHubFixture>, IAsyncLifetime
{
    private readonly ChatHubFixture _fixture;
    private readonly IDatabase _db;
    private string? _testToken;

    public ChatHubTests(ChatHubFixture fixture)
    {
        _fixture = fixture;
        _db = fixture.Factory.Services.GetRequiredService<IDatabase>();
        // In a real test, you would generate a valid JWT token here. For simplicity, we'll just use a placeholder string.
        _testToken = "test-jwt-token";
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
            .WithUrl(
                $"{server.BaseAddress}chathub",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult(_testToken);
                })
            .Build();
    }

    [Fact]
    public async Task Connect_AddsUserToActiveUsers()
    {
        var connection = CreateHubConnection("user-1", "Alice");

        // set up a listener to know when the server finishes processing hte connection
        var connectedTaskCompletionSource = new TaskCompletionSource();
        connection.On<object>("ActiveUsers", _ => connectedTaskCompletionSource.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);

        await connectedTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var isAdded = await _db.SetContainsAsync("chat:active_users:user-1", connection.ConnectionId);
        Assert.True(isAdded, "User should be added to active users in Redis");

        await connection.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Disconnect_RemovesUserFromActiveUsers()
    {
        var connection = CreateHubConnection("user-1", "Alice");

        var connectedTaskCompletionSource = new TaskCompletionSource();
        connection.On<object>("ActiveUsers", _ => connectedTaskCompletionSource.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connectedTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(await _db.SetContainsAsync("chat:active_users:user-1", connection.ConnectionId));

        var tempConnectionId = connection.ConnectionId;

        await connection.StopAsync(TestContext.Current.CancellationToken);

        // poll for a short time until OnDisconnectedAsync completes
        bool isRemoved = false;
        for (int i = 0; i < 10; i++)
        {
            if (!await _db.SetContainsAsync("chat:active_users:user-1", tempConnectionId))
            {
                isRemoved = true;
                break;
            }
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        Assert.True(isRemoved);
    }

    [Fact]
    public async Task SendMessage_StoresMessageInRedis()
    {
        var connection = CreateHubConnection("user-1", "Alice");
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var messageText = "Hello, world!";
        await connection.InvokeAsync("SendMessage", messageText, TestContext.Current.CancellationToken);

        var messages = await _db.ListRangeAsync("chat:messages", 0, -1);
        Assert.Single(messages);

        var payload = messages[0].ToString();
        Assert.Contains(messageText, payload);
        Assert.Contains("user-1", payload);

        await connection.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Connect_ReceivesMessageHistory()
    {
        await _db.ListLeftPushAsync("chat:messages", "{\"userId\":\"user-2\",\"UserName\":\"Bob\",\"text\":\"Hi Alice!\",\"timestamp\":1234567890}");

        var historyTaskCompletionSource = new TaskCompletionSource<object>();
        var connection = CreateHubConnection("user-1", "Alice");

        connection.On<object>("MessageHistory", history =>
        {
            historyTaskCompletionSource.SetResult(history);
        });

        await connection.StartAsync(TestContext.Current.CancellationToken);

        var historyResult = await historyTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.NotNull(historyResult);

        await connection.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Connect_ReceivesActiveUsers()
    {
        var joinedUserTaskCompletionSource = new TaskCompletionSource<(string userId, string userName)>();
        var leftUserTaskCompletionSource = new TaskCompletionSource<string>();

        // client 1 connects and listens for events
        var connection1 = CreateHubConnection("user-1", "Alice");
        connection1.On<string, string>("UserJoined", (userId, userName) => joinedUserTaskCompletionSource.SetResult((userId, userName)));
        connection1.On<string>("UserLeft", userId => leftUserTaskCompletionSource.SetResult(userId));

        await connection1.StartAsync(TestContext.Current.CancellationToken);

        // client 2 connects
        var connection2 = CreateHubConnection("user-2", "Bob");
        await connection2.StartAsync(TestContext.Current.CancellationToken);

        // assert join
        var joinedUserId = await joinedUserTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("user-2", joinedUserId.userId);

        // client 2 disconnects
        await connection2.StopAsync(TestContext.Current.CancellationToken);

        // Assert leave
        var leftUserId = await leftUserTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("user-2", leftUserId);

        await connection1.StopAsync(TestContext.Current.CancellationToken);
    }
}