using StackExchange.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

[Authorize]
public class ChatHub(IDatabase redis, ILogger<ChatHub> logger) : Hub
{
    private const string UsersKey = "chat:active_users";
    private const string MessagesKey = "chat:messages";
    private const int MaxMessages = 100;

    public async Task SendMessage(string message)
    {
        var userId = Context.UserIdentifier ?? "Unknown";
        var userName = Context.User?.Identity?.Name ?? "Unknown";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var payload = JsonSerializer.Serialize(new { userId, username = userName, text = message, timestamp });

        await redis.ListLeftPushAsync(MessagesKey, payload);
        await redis.ListTrimAsync(MessagesKey, 0, MaxMessages - 1);

        await Clients.All.SendAsync("ReceiveMessage", userId, userName, message, timestamp);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? "Unknown";
        var userName = Context.User?.Identity?.Name ?? "Unknown";
        logger.LogInformation("User connected: {UserId} ({UserName})", userId, userName);

        await redis.HashSetAsync(UsersKey, userId, userName);

        var entries = await redis.HashGetAllAsync(UsersKey);
        var activeUsers = entries.Select(e => new { userId = e.Name.ToString(), username = e.Value.ToString() });
        await Clients.Caller.SendAsync("ActiveUsers", activeUsers);

        await Clients.Others.SendAsync("UserJoined", userId, userName);

        var history = await redis.ListRangeAsync(MessagesKey, 0, MaxMessages - 1);
        var replayMessages = history
            .Select(m => JsonSerializer.Deserialize<object>((string)m!))
            .Reverse()
            .ToList();
        await Clients.Caller.SendAsync("MessageHistory", replayMessages);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        if (exception is not null)
            logger.LogError(exception, "User disconnected with error: {UserId}", userId);
        else
            logger.LogInformation("User disconnected: {UserId}", userId);

        await redis.HashDeleteAsync(UsersKey, userId);
        await Clients.Others.SendAsync("UserLeft", userId);

        await base.OnDisconnectedAsync(exception);
    }
}