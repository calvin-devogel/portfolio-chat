using StackExchange.Redis;
using PortfolioChat.Services;

namespace PortfolioChat;
public class Program
{
    public static async Task Main(string[] args)
    {
        var logger = new LogService(new LoggerFactory().CreateLogger<LogService>());
        var builder = WebApplication.CreateBuilder(args);

        var configService = new ConfigService(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(configService);

        builder.Services.AddValkeyService(configService);

        builder.Services.AddAuthService(configService);

        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();
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