using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace portfolio_chat.Tests;

public class ChatWebApplicationFactory : WebApplicationFactory<PortfolioChat.Program> {
    private const int TestDatabaseIndex = 2;
    private readonly string _redisConnectionString;

    public ChatWebApplicationFactory(string redisConnectionString = "localhost:6379,allowAdmin=true") {
        _redisConnectionString = redisConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseSetting("redis_uri", _redisConnectionString);
        builder.UseSetting("Redis:DatabaseIndex", TestDatabaseIndex.ToString());

        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubKeyBytes = ec.ExportSubjectPublicKeyInfo();
        var pemKey = new string(PemEncoding.Write("PUBLIC KEY", pubKeyBytes));

        builder.UseSetting("Application:JWT_PUBLIC_KEY", pemKey);
        builder.UseSetting("Application:JWT_ISSUER", "test-issuer");

        builder.ConfigureServices(services => {
            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }
}