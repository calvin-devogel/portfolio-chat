using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class ChatHub : Hub
{
    public async Task SendMessage(string message)
    {
        var userId = Context.UserIdentifier ?? "Unknown";
        await Clients.All.SendAsync("ReceiveMessage", userId, message);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ReceiveMessage", "System", "Connected!");
        await base.OnConnectedAsync();
    }
}