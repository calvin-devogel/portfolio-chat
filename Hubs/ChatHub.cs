using StackExchange.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Serilog.Context;

[Authorize]
public partial class ChatHub(IDatabase redis, ILogger<ChatHub> logger) : Hub {
    private const string UsersKey = "chat:active_users";
    private const string MessagesKey = "chat:messages";
    private const int MaxMessages = 100;
    private const int MaxMessageLength = 500;
    public record ChatMessagePayload(string userId, string username, string text, long timestamp);

    private static List<ChatMessagePayload>? _messageCache;
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received message from {UserId} ({UserName}), length={MessageLength}")]
    private partial void LogMessageReceived(string userId, string userName, int messageLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "User connected: {UserId} ({UserName})")]
    private partial void LogUserConnected(string userId, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "User disconnected: {UserId}")]
    private partial void LogUserDisconnected(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "User disconnected with error: {UserId}")]
    private partial void LogUserDisconnectedWithError(string userId);

    public async Task SendMessage(string message) {
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
            return;

        var userId = Context.UserIdentifier ?? "Unknown";
        var userName = Context.User?.Identity?.Name ?? "Unknown";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        LogMessageReceived(userId, userName, message.Length);

        var payload = JsonSerializer.Serialize(new { userId, username = userName, text = message, timestamp });

        await redis.ListLeftPushAsync(MessagesKey, payload);
        await redis.ListTrimAsync(MessagesKey, 0, MaxMessages - 1);

        if (_messageCache != null) {
            await _cacheLock.WaitAsync();
            try {
                if (_messageCache != null) {
                    _messageCache.Add(new ChatMessagePayload(userId, userName, message, timestamp));
                    if (_messageCache.Count > MaxMessages) {
                        _messageCache.RemoveAt(0); // evict
                    }
                }
            }
            finally {
                _cacheLock.Release();
            }
        }

        await Clients.All.SendAsync("ReceiveMessage", userId, userName, message, timestamp);
    }

    public override async Task OnConnectedAsync() {
        var userId = Context.UserIdentifier
            ?? throw new InvalidOperationException("UserIdentifier is required for authentication");
        var userName = Context.User?.Identity?.Name ?? "Unknown";

        using (LogContext.PushProperty("ConnectionId", Context.ConnectionId))
        using (LogContext.PushProperty("UserId", userId)) {
            LogUserConnected(userId, userName);

            await redis.SetAddAsync($"chat:active_users:{userId}", Context.ConnectionId);
            await redis.HashSetAsync(UsersKey, userId, userName);

            var entries = await redis.HashGetAllAsync(UsersKey);
            var activeUsers = entries.Select(e => new { userId = e.Name.ToString(), username = e.Value.ToString() });
            await Clients.Caller.SendAsync("ActiveUsers", activeUsers);
            await Clients.Others.SendAsync("UserJoined", userId, userName);

            List<ChatMessagePayload> replayMessages;
            await _cacheLock.WaitAsync();
            try {
                if (_messageCache == null) {
                    var history = await redis.ListRangeAsync(MessagesKey, 0, MaxMessages - 1);
                    _messageCache = [.. history
                        .Select(m => JsonSerializer.Deserialize<ChatMessagePayload>((byte[])m!))
                        .Where(m => m is not null) // Filter out any nulls
                        .Select(m => m!) // Satisfy compiler
                        .Reverse()];
                }

                replayMessages = _messageCache.ToList();
            }
            finally {
                _cacheLock.Release();
            }

            await Clients.Caller.SendAsync("MessageHistory", replayMessages);
            await base.OnConnectedAsync();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception) {
        var userId = Context.UserIdentifier ??
            throw new InvalidOperationException("UserIdentifier is required for authentication");
        if (exception is not null)
            LogUserDisconnectedWithError(userId);
        else
            LogUserDisconnected(userId);

        await redis.SetRemoveAsync($"chat:active_users:{userId}", Context.ConnectionId);
        var remaining = await redis.SetLengthAsync($"chat:active_users:{userId}");
        if (remaining == 0) {
            await redis.HashDeleteAsync(UsersKey, userId);
            await Clients.Others.SendAsync("UserLeft", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}