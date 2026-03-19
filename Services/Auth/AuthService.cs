using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PortfolioChat.Services.Auth;
public class AuthService
{
    private readonly ECDsaSecurityKey _key;
    private readonly string _issuer;
    private readonly JwtBearerOptions _jwtOptions;

    public AuthService(Config.ConfigService config)
    {
        var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ec.ImportFromPem(config.JwtPublicKeyPem);
        this._key = new ECDsaSecurityKey(ec);
        this._issuer = config.JwtIssuer;

        this._jwtOptions = new JwtBearerOptions
        {
            TokenValidationParameters = new TokenValidationParameters
            {
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
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearer");
                    logger.LogError(
                        context.Exception,
                        "JWT authentication failed: {ExceptionType}",
                        context.Exception.GetType().Name
                    );
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearer");
                    logger.LogWarning(
                        "JWT challenge issued: Error={Error}, Description={Description}",
                        context.Error, context.ErrorDescription
                    );
                    return Task.CompletedTask;
                }
            }
        };
    }

    public JwtBearerOptions GetJwtOptions() => _jwtOptions;
}
