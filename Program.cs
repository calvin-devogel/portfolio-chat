using StackExchange.Redis;
using PortfolioChat.Services;
using Microsoft.AspNetCore.SignalR;

namespace PortfolioChat;
public class Program
{
    public static async Task Main(string[] args)
    {
        var authLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AuthService>();
        var valkeyLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ValkeyService>();
        var builder = WebApplication.CreateBuilder(args);

        var configService = new ConfigService(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(configService);

        builder.Services.AddValkeyService(configService, valkeyLogger);

        builder.Services.AddAuthService(configService, authLogger);

        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
        builder.Services.AddCors(options => options.AddPolicy("PortfolioPolicy", configService.GetCorsPolicy("PortfolioPolicy")));

        var app = builder.Build();
        var db = app.Services.GetRequiredService<IDatabase>();
        await db.KeyDeleteAsync("chat:active_users");

        app.UseCors("PortfolioPolicy");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<ChatHub>("/chathub");

        app.Run();
    }
}