using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using PortfolioChat.Services.Auth;
using PortfolioChat.Services.Config;

var builder = WebApplication.CreateBuilder(args);

var configService = new ConfigService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(configService);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(configService.RedisConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(configService.RedisDatabaseIndex));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authService = new AuthService(configService);
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