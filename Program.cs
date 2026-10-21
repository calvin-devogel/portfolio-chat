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
        using var bootstrapLogger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        try {
            var builder = WebApplication.CreateBuilder(args);
            var redisUriSetting = builder.Configuration["redis_uri"];
            builder.Configuration.AddEnvironmentVariables("APP_");
            // Preserve explicit host settings, such as the test factory's unavailable endpoint.
            if (redisUriSetting is not null) {
                builder.Configuration["redis_uri"] = redisUriSetting;
            }

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new CompactJsonFormatter()),
                preserveStaticLogger: true);

            var configService = new ConfigService(builder.Configuration);
            builder.Services.AddSingleton(configService);

            var chatEnabled = await builder.Services.TryAddValkeyService(configService);
            builder.Services.AddAuthService(configService);

            builder.Services.AddAuthorization();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
            builder.Services.AddCors(options =>
                options.AddPolicy("PortfolioPolicy", configService.GetCorsPolicy("PortfolioPolicy")));

            var app = builder.Build();
            RuntimeHelpers.RunClassConstructor(typeof(ChatHub).TypeHandle);

            if (chatEnabled && app.Environment.IsDevelopment()) {
                var db = app.Services.GetRequiredService<IDatabase>();
                app.Logger.LogInformation("Clearing active users from Valkey in Dev...");
                await db.KeyDeleteAsync("chat:active_users");
            }
            else if (chatEnabled) {
                app.Logger.LogInformation("Skipping Redis active users cleanup outside Dev...");
            }
            else {
                app.Logger.LogWarning("Chat endpoints are disabled because Valkey is unavailable");
            }

            app.UseCors("PortfolioPolicy");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHttpMetrics();
            app.MapMetrics();
            if (chatEnabled) {
                app.MapHub<ChatHub>("/ws/chat");
            }
            else {
                app.Map("/ws/chat", chatApp => chatApp.Run(async context => {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new {
                        code = "feature_unavailable",
                        message = "Chat is temporarily unavailable because session storage is offline."
                    });
                }));
            }

            app.Run();
        }
        catch (Exception ex) {
            bootstrapLogger.Fatal(ex, "Application terminated unexpectedly");
        }
    }
}