using StackExchange.Redis;
using PortfolioChat.Services;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Formatting.Compact;
using Prometheus;
using System.Runtime.CompilerServices;

namespace PortfolioChat;

public class Program {
    public static async Task Main(string[] args) {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();
        try {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables("APP_");

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new CompactJsonFormatter()));

            var configService = new ConfigService(builder.Configuration);
            builder.Services.AddSingleton(configService);

            builder.Services.AddValkeyService(configService);
            builder.Services.AddAuthService(configService);

            builder.Services.AddAuthorization();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
            builder.Services.AddCors(options =>
                options.AddPolicy("PortfolioPolicy", configService.GetCorsPolicy("PortfolioPolicy")));

            var app = builder.Build();
            RuntimeHelpers.RunClassConstructor(typeof(ChatHub).TypeHandle);

            if (app.Environment.IsDevelopment()) {
                var db = app.Services.GetRequiredService<IDatabase>();
                app.Logger.LogInformation("Clearing active users from Valkey in Dev...");
                await db.KeyDeleteAsync("chat:active_users");
            }
            else {
                app.Logger.LogInformation("Skipping Redis active users cleanup outside Dev...");
            }

            app.UseCors("PortfolioPolicy");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHttpMetrics();
            app.MapMetrics();
            app.MapHub<ChatHub>("/ws/chat");

            app.Run();
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally {
            await Log.CloseAndFlushAsync();
        }
    }
}