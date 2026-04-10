using Microsoft.AspNetCore.Cors.Infrastructure;

namespace PortfolioChat.Services;

public class ConfigService {
    public readonly string _jwtPublicKeyPem;
    public readonly string _jwtIssuer;
    public readonly string _jwtAudience;
    public readonly string[] _allowedOrigins;
    public readonly string _redisConnectionString;
    public readonly int _redisDatabaseIndex;
    public readonly CorsOptions _corsOptions;

    public ConfigService(IConfiguration configuration) {

        var jwtPublicKeyPem = configuration["Application:JWT_PUBLIC_KEY"]
            ?? throw new InvalidOperationException("APP_APPLICATION__JWT_PUBLIC_KEY configuration is required");

        _jwtPublicKeyPem = jwtPublicKeyPem;
        _jwtIssuer = configuration["Application:JWT_ISSUER"] ?? "portfolio-server";
        _jwtAudience = configuration["Application:JWT_AUDIENCE"] ?? "portfolio-chat";
        _allowedOrigins = configuration.GetSection("Cors:ALLOWED_ORIGINS").Get<string[]>()
            ?? throw new InvalidOperationException("APP_CORS__ALLOWED_ORIGINS configuration is required");
        _redisConnectionString = configuration["redis_uri"]
            ?? throw new InvalidOperationException("APP_REDIS_URI is required");
        _redisDatabaseIndex = configuration.GetValue("Redis:DatabaseIndex", 1);

        _corsOptions = new CorsOptions();
        _corsOptions.AddPolicy("PortfolioPolicy", policy =>
            policy.WithOrigins(_allowedOrigins)
                .WithMethods("GET", "POST")
                .WithHeaders("Authorization", "Content-Type")
                .AllowCredentials());

    }

    public string JwtPublicKeyPem => _jwtPublicKeyPem;
    public string JwtIssuer => _jwtIssuer;
    public string JwtAudience => _jwtAudience;
    public string[] AllowedOrigins => _allowedOrigins;
    public string RedisConnectionString => _redisConnectionString;
    public int RedisDatabaseIndex => _redisDatabaseIndex;
    public CorsOptions CorsOptions => _corsOptions;
    public CorsPolicy GetCorsPolicy(string policyName) => _corsOptions.GetPolicy(policyName)
        ?? throw new InvalidOperationException($"CORS policy '{policyName}' not found");
}