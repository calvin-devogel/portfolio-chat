using Microsoft.AspNetCore.Cors.Infrastructure;

namespace PortfolioChat.Services;

public class ConfigService
{
    public readonly string _jwtPublicKeyPem;
    public readonly string _jwtIssuer;
    public readonly string[] _allowedOrigins;
    public readonly string _redisConnectionString;
    public readonly int _redisDatabaseIndex;
    public readonly CorsOptions _corsOptions;

    public ConfigService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var publicKeyPath = configuration["Jwt:PublicKeyPath"]
            ?? throw new InvalidOperationException(
                "Jwt:PublicKeyPath configuration is required"
                );

        var resolvedJwtPublicKeyPath = Path.IsPathRooted(publicKeyPath)
            ? publicKeyPath
            : Path.Combine(environment.ContentRootPath, publicKeyPath);

        var jwtPublicKeyPem = File.ReadAllText(resolvedJwtPublicKeyPath);

        _jwtPublicKeyPem = jwtPublicKeyPem;
        _jwtIssuer = configuration["Jwt:Issuer"] ?? "portfolio-server";
        _allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "http://localhost:5173", "http://localhost:8000" };
        _redisConnectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString configuration is required");
        _redisDatabaseIndex = configuration.GetValue<int>("Redis:DatabaseIndex", 1);

        _corsOptions = new CorsOptions();
        _corsOptions.AddPolicy("PortfolioPolicy", policy =>
            policy.WithOrigins(_allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());

    }

    public string JwtPublicKeyPem => _jwtPublicKeyPem;
    public string JwtIssuer => _jwtIssuer;
    public string[] AllowedOrigins => _allowedOrigins;
    public string RedisConnectionString => _redisConnectionString;
    public int RedisDatabaseIndex => _redisDatabaseIndex;
    public CorsOptions CorsOptions => _corsOptions;
    public CorsPolicy GetCorsPolicy(string policyName) => _corsOptions.GetPolicy(policyName)
        ?? throw new InvalidOperationException($"CORS policy '{policyName}' not found");
}