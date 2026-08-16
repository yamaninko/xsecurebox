using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class ApiClientService : IApiClientService
{
    private readonly SecureBoxDbContext _dbContext;
    private readonly ILogger<ApiClientService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;

    public ApiClientService(
        SecureBoxDbContext dbContext,
        IConfiguration configuration,
        ILogger<ApiClientService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        var jwtSection = configuration.GetSection("JwtSettings");
        var jwtKey = jwtSection["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        _issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        _audience = jwtSection["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
        _accessTokenExpirationMinutes = int.TryParse(jwtSection["AccessTokenExpirationMinutes"], out var minutes)
            ? minutes
            : 15;
    }

    public async Task<IEnumerable<ApiClientDto>> GetAllClientsAsync()
    {
        var clients = await _dbContext.ApiClients
            .Include(c => c.Creator)
            .Where(c => c.RevokedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return clients.Select(MapToDto);
    }

    public async Task<ApiClientDto?> GetClientByIdAsync(Guid clientId)
    {
        var client = await _dbContext.ApiClients
            .Include(c => c.Creator)
            .FirstOrDefaultAsync(c => c.ClientId == clientId);

        return client == null ? null : MapToDto(client);
    }

    public async Task<(ApiClientDetailDto client, string clientSecret)> CreateClientAsync(
        CreateApiClientRequest request, 
        Guid createdBy)
    {
        // Generate Client ID (unique string)
        var clientIdString = GenerateClientId();
        
        // Generate Client Secret (random secure string)
        var clientSecret = GenerateClientSecret();
        var clientSecretHash = BCrypt.Net.BCrypt.HashPassword(clientSecret);
        
        // Generate API Key
        var apiKey = GenerateApiKey();

        var client = new ApiClient
        {
            ClientName = request.ClientName,
            Description = request.Description,
            ClientIdString = clientIdString,
            ClientSecretHash = clientSecretHash,
            ApiKey = apiKey,
            Scopes = JsonSerializer.Serialize(request.Scopes),
            RateLimitPerMinute = request.RateLimitPerMinute,
            RateLimitPerHour = request.RateLimitPerHour,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ApiClients.Add(client);
        await _dbContext.SaveChangesAsync();

        // Reload with navigation properties
        await _dbContext.Entry(client).Reference(c => c.Creator).LoadAsync();

        var clientDto = new ApiClientDetailDto(
            client.ClientId,
            client.ClientName,
            client.Description,
            client.ClientIdString,
            client.ApiKey,
            JsonSerializer.Deserialize<List<string>>(client.Scopes) ?? new List<string>(),
            client.RateLimitPerMinute,
            client.RateLimitPerHour,
            client.IsActive,
            client.LastUsedAt,
            client.TotalRequests,
            client.Creator?.Username ?? "Unknown",
            client.CreatedAt
        );

        return (clientDto, clientSecret);
    }

    public async Task<ApiClientDto> UpdateClientAsync(Guid clientId, UpdateApiClientRequest request)
    {
        var client = await _dbContext.ApiClients.FindAsync(clientId);
        if (client == null) throw new KeyNotFoundException("API Client not found");

        if (request.ClientName != null) client.ClientName = request.ClientName;
        if (request.Description != null) client.Description = request.Description;
        if (request.Scopes != null) client.Scopes = JsonSerializer.Serialize(request.Scopes);
        if (request.RateLimitPerMinute.HasValue) client.RateLimitPerMinute = request.RateLimitPerMinute.Value;
        if (request.RateLimitPerHour.HasValue) client.RateLimitPerHour = request.RateLimitPerHour.Value;
        if (request.IsActive.HasValue) client.IsActive = request.IsActive.Value;

        client.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _dbContext.Entry(client).Reference(c => c.Creator).LoadAsync();
        return MapToDto(client);
    }

    public async Task RevokeClientAsync(Guid clientId, string reason, Guid revokedBy)
    {
        var client = await _dbContext.ApiClients.FindAsync(clientId);
        if (client == null) throw new KeyNotFoundException("API Client not found");

        client.IsActive = false;
        client.RevokedAt = DateTime.UtcNow;
        client.RevokedBy = revokedBy;
        client.RevokedReason = reason;

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteClientAsync(Guid clientId)
    {
        var client = await _dbContext.ApiClients.FindAsync(clientId);
        if (client == null) throw new KeyNotFoundException("API Client not found");

        _dbContext.ApiClients.Remove(client);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<ApiClientSecretResponse> RegenerateClientSecretAsync(Guid clientId)
    {
        var client = await _dbContext.ApiClients.FindAsync(clientId);
        if (client == null) throw new KeyNotFoundException("API Client not found");

        var newSecret = GenerateClientSecret();
        client.ClientSecretHash = BCrypt.Net.BCrypt.HashPassword(newSecret);
        client.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return new ApiClientSecretResponse(client.ClientIdString, newSecret);
    }

    public async Task<string> RegenerateApiKeyAsync(Guid clientId)
    {
        var client = await _dbContext.ApiClients.FindAsync(clientId);
        if (client == null) throw new KeyNotFoundException("API Client not found");

        var newApiKey = GenerateApiKey();
        client.ApiKey = newApiKey;
        client.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return newApiKey;
    }

    public async Task<OAuthTokenResponse> GenerateAccessTokenAsync(string clientId, string clientSecret, string? scope)
    {
        var client = await _dbContext.ApiClients
            .FirstOrDefaultAsync(c => c.ClientIdString == clientId && c.IsActive);

        if (client == null)
            throw new UnauthorizedAccessException("Invalid client credentials");

        // Verify client secret
        if (!BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecretHash))
            throw new UnauthorizedAccessException("Invalid client credentials");

        // Parse requested scopes
        var requestedScopes = scope?.Split(' ') ?? Array.Empty<string>();
        var clientScopes = JsonSerializer.Deserialize<List<string>>(client.Scopes) ?? new List<string>();
        
        // Validate requested scopes
        var grantedScopes = requestedScopes.Where(s => clientScopes.Contains(s)).ToList();
        if (grantedScopes.Count == 0)
            grantedScopes = clientScopes; // Grant all scopes if none requested

        // Generate JWT token
        var claims = new List<Claim>
        {
            new Claim("client_id", client.ClientIdString),
            new Claim(JwtRegisteredClaimNames.Sub, client.ClientId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("token_type", "access"),
            new Claim(ClaimTypes.Role, "Service"),
            new Claim("role", "Service")
        };

        foreach (var scopeItem in grantedScopes)
        {
            claims.Add(new Claim("scope", scopeItem));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature),
            Issuer = _issuer,
            Audience = _audience
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(token);

        // Update last used
        client.LastUsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return new OAuthTokenResponse(
            tokenString,
            "Bearer",
            _accessTokenExpirationMinutes * 60,
            string.Join(" ", grantedScopes)
        );
    }

    public async Task<Guid?> ValidateApiKeyAsync(string apiKey)
    {
        var client = await _dbContext.ApiClients
            .FirstOrDefaultAsync(c => c.ApiKey == apiKey && c.IsActive && c.RevokedAt == null);

        if (client != null)
        {
            client.LastUsedAt = DateTime.UtcNow;
            client.TotalRequests++;
            await _dbContext.SaveChangesAsync();
        }

        return client?.ClientId;
    }

    public async Task<Guid?> ValidateBearerTokenAsync(string token)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = _signingKey,
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            var clientIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "sub");
            
            if (clientIdClaim != null && Guid.TryParse(clientIdClaim.Value, out var clientId))
            {
                var client = await _dbContext.ApiClients.FindAsync(clientId);
                if (client != null && client.IsActive && client.RevokedAt == null)
                {
                    client.LastUsedAt = DateTime.UtcNow;
                    client.TotalRequests++;
                    await _dbContext.SaveChangesAsync();
                    return clientId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
        }

        return null;
    }

    public async Task<ApiClientStatsDto> GetClientStatsAsync()
    {
        var totalClients = await _dbContext.ApiClients.CountAsync();
        var activeClients = await _dbContext.ApiClients.CountAsync(c => c.IsActive && c.RevokedAt == null);
        var revokedClients = await _dbContext.ApiClients.CountAsync(c => c.RevokedAt != null);
        var totalRequests = await _dbContext.ApiClients.SumAsync(c => c.TotalRequests);
        
        var today = DateTime.UtcNow.Date;
        var requestsToday = await _dbContext.ApiClientRequests
            .Where(r => r.RequestedAt >= today)
            .CountAsync();

        var topClients = await _dbContext.ApiClients
            .OrderByDescending(c => c.TotalRequests)
            .Take(10)
            .Select(c => new ClientRequestStatsDto(
                c.ClientName,
                c.TotalRequests,
                c.LastUsedAt
            ))
            .ToListAsync();

        return new ApiClientStatsDto(
            totalClients,
            activeClients,
            revokedClients,
            totalRequests,
            requestsToday,
            topClients
        );
    }

    public async Task LogClientRequestAsync(Guid clientId, string endpoint, string method, int statusCode, string? ipAddress)
    {
        var request = new ApiClientRequest
        {
            ClientId = clientId,
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            IpAddress = ipAddress,
            RequestedAt = DateTime.UtcNow
        };

        _dbContext.ApiClientRequests.Add(request);
        await _dbContext.SaveChangesAsync();
    }

    // Helper Methods
    private static string GenerateClientId()
    {
        // Format: cli_xxxxxxxxxxxxxxxxxxxxxxxx (28 characters)
        return "cli_" + GenerateRandomString(24);
    }

    private static string GenerateClientSecret()
    {
        // Format: cs_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx (51 characters)
        return "cs_" + GenerateRandomString(48);
    }

    private static string GenerateApiKey()
    {
        // Format: sk_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx (51 characters)
        return "sk_" + GenerateRandomString(48);
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var data = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(data);
        }
        return new string(data.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static ApiClientDto MapToDto(ApiClient client)
    {
        return new ApiClientDto(
            client.ClientId,
            client.ClientName,
            client.Description,
            client.ClientIdString,
            JsonSerializer.Deserialize<List<string>>(client.Scopes) ?? new List<string>(),
            client.RateLimitPerMinute,
            client.RateLimitPerHour,
            client.IsActive,
            client.LastUsedAt,
            client.TotalRequests,
            client.Creator?.Username ?? "Unknown",
            client.CreatedAt,
            client.RevokedAt
        );
    }
}

