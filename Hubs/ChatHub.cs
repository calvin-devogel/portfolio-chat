using StackExchange.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Serilog.Context;

[Authorize]
public partial class ChatHub(IDatabase redis, ILogger<ChatHub> logger) : Hub
{
    private const string UsersKey = "chat:active_users";
    private const string MessagesKey = "chat:messages";
    private const int MaxMessages = 100;

    [LoggerMessage(Level = LogLevel.Information, Message = "Received message from {UserId} ({UserName}), length={MessageLength}")]
    private partial void LogMessageReceived(string userId, string userName, int messageLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "User connected: {UserId} ({UserName})")]
    private partial void LogUserConnected(string userId, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "User disconnected: {UserId}")]
    private partial void LogUserDisconnected(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "User disconnected with error: {UserId}")]
    private partial void LogUserDisconnectedWithError(string userId);

    public async Task SendMessage(string message)
    {
        var userId = Context.UserIdentifier ?? "Unknown";
        var userName = Context.User?.Identity?.Name ?? "Unknown";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        LogMessageReceived(userId, userName, message.Length);

        var payload = JsonSerializer.Serialize(new { userId, username = userName, text = message, timestamp });

        await redis.ListLeftPushAsync(MessagesKey, payload);
        await redis.ListTrimAsync(MessagesKey, 0, MaxMessages - 1);

        await Clients.All.SendAsync("ReceiveMessage", userId, userName, message, timestamp);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier
            ?? throw new InvalidOperationException("UserIdentifier is required for authentication");
        var userName = Context.User?.Identity?.Name ?? "Unknown";

        using (LogContext.PushProperty("UserId", Context.ConnectionId))
        using (LogContext.PushProperty("UserId", userId))
        {
            LogUserConnected(userId, userName);

            await redis.SetAddAsync($"chat:active_users:{userId}", Context.ConnectionId);

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
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ??
            throw new InvalidOperationException("UserIdentifier is required for authentication");
        if (exception is not null)
            LogUserDisconnectedWithError(userId);
        else
            LogUserDisconnected(userId);

        await redis.SetRemoveAsync($"chat:active_users:{userId}", Context.ConnectionId);
        await Clients.Others.SendAsync("UserLeft", userId);

        await base.OnDisconnectedAsync(exception);
    }
}