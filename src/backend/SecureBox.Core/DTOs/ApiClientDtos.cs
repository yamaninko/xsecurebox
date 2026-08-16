using System;
using System.Collections.Generic;

namespace SecureBox.Core.DTOs;

// Response DTOs
public record ApiClientDto(
    Guid ClientId,
    string ClientName,
    string? Description,
    string ClientIdString,
    List<string> Scopes,
    int RateLimitPerMinute,
    int RateLimitPerHour,
    bool IsActive,
    DateTime? LastUsedAt,
    long TotalRequests,
    string CreatedByUsername,
    DateTime CreatedAt,
    DateTime? RevokedAt
);

public record ApiClientDetailDto(
    Guid ClientId,
    string ClientName,
    string? Description,
    string ClientIdString,
    string ApiKey, // Only shown once after creation or regeneration
    List<string> Scopes,
    int RateLimitPerMinute,
    int RateLimitPerHour,
    bool IsActive,
    DateTime? LastUsedAt,
    long TotalRequests,
    string CreatedByUsername,
    DateTime CreatedAt
);

public record ApiClientSecretResponse(
    string ClientId,
    string ClientSecret // Plain text - only shown once!
);

// Request DTOs
public record CreateApiClientRequest(
    string ClientName,
    string? Description,
    List<string> Scopes,
    int RateLimitPerMinute = 60,
    int RateLimitPerHour = 1000
);

public record UpdateApiClientRequest(
    string? ClientName,
    string? Description,
    List<string>? Scopes,
    int? RateLimitPerMinute,
    int? RateLimitPerHour,
    bool? IsActive
);

public record RegenerateSecretRequest();

public record RegenerateApiKeyRequest();

// OAuth Token Request/Response
public record TokenRequest(
    string grant_type, // "client_credentials"
    string client_id,
    string client_secret,
    string? scope
);

public record OAuthTokenResponse(
    string access_token,
    string token_type, // "Bearer"
    int expires_in, // seconds
    string scope
);

// API Client Statistics
public record ApiClientStatsDto(
    int TotalClients,
    int ActiveClients,
    int RevokedClients,
    long TotalRequests,
    long RequestsToday,
    List<ClientRequestStatsDto> TopClients
);

public record ClientRequestStatsDto(
    string ClientName,
    long RequestCount,
    DateTime? LastUsedAt
);

