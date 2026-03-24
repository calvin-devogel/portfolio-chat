using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace portfolio_chat.Tests;

public class ChatWebApplicationFactory : WebApplicationFactory<PortfolioChat.Program>
{
    private const int TestDatabaseIndex = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", "localhost:6379");
        builder.UseSetting("Redis:DatabaseIndex", TestDatabaseIndex.ToString());

        var tempKeyPath = Path.Combine(Path.GetTempPath(), "test-ecdsa-public-key.pem");
        if (!File.Exists(tempKeyPath))
        {
            using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var pubKeyBytes = ec.ExportSubjectPublicKeyInfo();
            var pemKey = new string(PemEncoding.Write("PUBLIC KEY", pubKeyBytes));
            File.WriteAllText(tempKeyPath, pemKey);
        }

        builder.UseSetting("Jwt:PublicKeyPath", tempKeyPath);
        builder.UseSetting("Jwt:Issuer", "test-issuer");

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
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