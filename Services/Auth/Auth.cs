using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace PortfolioChat.Services;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var subClaim = connection.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(subClaim))
        {
            return subClaim;
        }

        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
public class AuthService
{
    private readonly ECDsaSecurityKey _key;
    private readonly string _issuer;
    private readonly JwtBearerOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ConfigService config, ILogger<AuthService> logger)
    {
        var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ec.ImportFromPem(config.JwtPublicKeyPem);
        this._key = new ECDsaSecurityKey(ec);
        this._issuer = config.JwtIssuer;
        this._logger = logger;

        this._jwtOptions = new JwtBearerOptions
        {
            TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
                RoleClaimType = "role",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
            },

            Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/chathub"))
                        context.Token = token;
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    _logger.LogError(
                        context.Exception,
                        "JWT authentication failed: {ExceptionType}",
                        context.Exception.GetType().Name
                    );
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    _logger.LogWarning(
                        "JWT challenge issued: Error={Error}, Description={Description}",
                        context.Error, context.ErrorDescription
                    );
                    return Task.CompletedTask;
                }
            }
        };
    }

    public void ConfigureJwtOptions(JwtBearerOptions options)
    {
        options.TokenValidationParameters = _jwtOptions.TokenValidationParameters;
        options.Events = _jwtOptions.Events;
    }
}

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthService(this IServiceCollection services, ConfigService config)
    {
        // We create the AuthService instance here to ensure the logger is properly injected and available
        // for configuration. This allows us to log any important information during the setup of JWT options.
        services.AddSingleton(sp =>
            new AuthService(config, sp.GetRequiredService<ILogger<AuthService>>()));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(); // add dummy options, configured below

        // Then we configure the JWT options using the instance of AuthService we just created.
        // This ensures that the logger is available for any logging within the configuration process.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>>(sp =>
            new ConfigureNamedOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options => sp.GetRequiredService<AuthService>().ConfigureJwtOptions(options)));
        return services;
    }
}