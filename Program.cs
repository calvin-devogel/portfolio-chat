using System.Security.Cryptography;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"]
    ?? throw new InvalidOperationException("Jwt:PublicKeyPath configuration is required");

var resolvedJwtPublicKeyPath = Path.IsPathRooted(publicKeyPath)
    ? publicKeyPath
    : Path.Combine(builder.Environment.ContentRootPath, publicKeyPath);

var jwtPublicKeyPem = File.ReadAllText(resolvedJwtPublicKeyPath);

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "portfolio-server";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "http://localhost:5173", "http://localhost:8000" };

var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
ec.ImportFromPem(jwtPublicKeyPem);

var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException("Redis:ConnectionString configuration is required");
var redisDatabaseIndex = builder.Configuration.GetValue<int>("Redis:DatabaseIndex", 1);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(redisDatabaseIndex));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new ECDsaSecurityKey(ec),
            ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
        // SignalR sends the token as '?access_token=...'
        // because WebSockets/SSE transports can't set custom headers
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if(!string.IsNullOrEmpty(token) && path.StartsWithSegments("/chathub"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("PortfolioPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseCors("PortfolioPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ChatHub>("/chathub");

app.Run();