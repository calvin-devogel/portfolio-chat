using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

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

    public JwtBearerOptions GetJwtOptions() => _jwtOptions;
    public JwtBearerOptions ConfigureJwtOptions()
    {
        var options = new JwtBearerOptions();
        options.TokenValidationParameters = _jwtOptions.TokenValidationParameters;
        options.Events = _jwtOptions.Events;

        return options;
    }
}

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthService(this IServiceCollection services, ConfigService config, ILogger<AuthService> logger)
    {
        var authService = new AuthService(config, logger);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => authService.ConfigureJwtOptions());
        return services;
    }
}