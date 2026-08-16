using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureBox.API.Authorization;
using SecureBox.API.Middleware;
using SecureBox.Core.Interfaces;
using SecureBox.Core.Validators;
using SecureBox.Infrastructure.Data;
using SecureBox.Infrastructure.Services;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/securebox-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value!.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => new ValidationErrorDetail
                {
                    Field = x.Key,
                    Message = e.ErrorMessage
                }))
                .ToList();

            var response = new ErrorResponse
            {
                Success = false,
                Error = new ErrorDetail
                {
                    Code = "VALIDATION_ERROR",
                    Message = "Validation failed",
                    Details = errors
                }
            };

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Secure Box API",
        Version = "v1",
        Description = "High-security key management system API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<SecureBoxDbContext>(options =>
{
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"));
    }
});

var isTesting = builder.Environment.IsEnvironment("Testing");
var redisConnection = builder.Configuration.GetConnectionString("Redis");
IConnectionMultiplexer? redisMux = null;

if (!isTesting && !string.IsNullOrWhiteSpace(redisConnection))
{
    try
    {
        redisMux = ConnectionMultiplexer.Connect(redisConnection);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redisMux);
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "SecureBox_";
        });
        builder.Services.AddSingleton<ITokenStore, RedisTokenStore>();
        builder.Services.AddSingleton<IRateLimitService, RedisRateLimitService>();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Redis unavailable; falling back to in-memory token and rate-limit stores");
        redisMux = null;
    }
}

if (redisMux is null)
{
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<ITokenStore, MemoryTokenStore>();
    builder.Services.AddSingleton<IRateLimitService, MemoryRateLimitService>();
}

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey) && !isTesting)
{
    throw new InvalidOperationException("JwtSettings:SecretKey is required (set JWT_SECRET_KEY)");
}

secretKey ??= "YourSuperSecretKeyMinimum32CharactersLongForHS256!";

SecureBox.API.Security.StartupSecrets.Validate(builder.Environment, builder.Configuration);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
        RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var tokenType = context.Principal?.FindFirst("token_type")?.Value;
            if (!string.Equals(tokenType, "access", StringComparison.OrdinalIgnoreCase))
            {
                context.Fail("Refresh tokens cannot be used as access tokens");
                return;
            }

            var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrWhiteSpace(jti))
            {
                return;
            }

            var store = context.HttpContext.RequestServices.GetRequiredService<ITokenStore>();
            if (await store.IsAccessTokenBlacklistedAsync(jti))
            {
                context.Fail("Token has been revoked");
            }
        }
    };
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionPolicies.All)
    {
        options.AddPolicy(permission, policy =>
            policy.Requirements.Add(new PermissionRequirement(permission)));
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPortal", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<IKeyService, KeyService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
if (isTesting || !builder.Configuration.GetValue<bool>("Ethereum:Enabled"))
{
    builder.Services.AddScoped<IChainVerificationService, DisabledChainVerificationService>();
}
else
{
    builder.Services.AddScoped<IChainVerificationService, EthereumVerificationService>();
}
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IApiClientService, ApiClientService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<ILifecycleService, LifecycleService>();
if (!isTesting)
{
    builder.Services.AddHostedService<SecureBox.API.Hosted.LifecycleHostedService>();
    if (builder.Configuration.GetValue<bool>("Ethereum:Enabled"))
    {
        builder.Services.AddHostedService<SecureBox.API.Hosted.EthereumBootstrapHostedService>();
    }
}

builder.Services.AddSingleton<SecureBox.API.Health.PostgresHealthCheck>();
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

if (!isTesting)
{
    healthChecks.AddCheck<SecureBox.API.Health.PostgresHealthCheck>("postgres", tags: new[] { "ready" });

    if (redisMux is not null)
    {
        var mux = redisMux;
        healthChecks.AddCheck("redis", () =>
            mux.IsConnected ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Redis disconnected"),
            tags: new[] { "ready" });
    }
}

var app = builder.Build();

var enableSwagger = app.Environment.IsDevelopment() ||
                    builder.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger && !app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Secure Box API V1");
    });
}

var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
    ForwardLimit = 2
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

var disableHttpsRedirect = builder.Configuration.GetValue<bool>("DisableHttpsRedirect");
if (!disableHttpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowPortal");
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready") || r.Tags.Contains("live")
});
app.MapHealthChecks("/health");

try
{
    await DatabaseInitializer.InitializeAsync(app.Services);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Database initialization failed");
    throw;
}

Log.Information("Secure Box API starting up...");

app.Run();

public partial class Program { }
