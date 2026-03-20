using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace portfolio_chat.Tests;

public class ChatWebApplicationFactory : WebApplicationFactory<PortfolioChat.Program>
{
    private const int TestDatabaseIndex = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", "localhost:6379");
        builder.UseSetting("Redis:DatabaseIndex", TestDatabaseIndex.ToString());

        builder.UseSetting("Jwt:PublicKeyPath", "unused");
        builder.UseSetting("Jwt:Issuer", "test-issuer");

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            var redisDescriptors = services
                .Where(d => d.ServiceType == typeof(IConnectionMultiplexer)
                    || d.ServiceType == typeof(IDatabase))
                .ToList();
            
            foreach (var d in redisDescriptors)
                services.Remove(d);
            
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect("localhost:6379"));
            services.AddSingleton(sp =>
                sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(TestDatabaseIndex)
            );
        });
    }
}