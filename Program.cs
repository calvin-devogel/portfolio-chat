using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace PortfolioChat;
public class Program
{
    public static async Task Main(string[] args)
    {
        var logger = new Services.LogService(new LoggerFactory().CreateLogger<Services.LogService>());
        var builder = WebApplication.CreateBuilder(args);

        var configService = new Services.ConfigService(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(configService);

        builder.Services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(configService.RedisConnectionString));
        builder.Services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(configService.RedisDatabaseIndex));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var authService = new Services.AuthService(configService);
                options = authService.GetJwtOptions();
            });

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