using StackExchange.Redis;
using System.Text.Json;

[Authorize]
public class ChatHub : Hub
{
    public async Task SendMessage(string message)
    {
        var userId = Context.UserIdentifier ?? "Unknown";
        var userName = Context.User?.Identity?.Name ?? "Unknown";
        await Clients.All.SendAsync("ReceiveMessage", userId, userName, message);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ReceiveMessage", "System", "Connected!");
        await base.OnConnectedAsync();
    }
}