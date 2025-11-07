using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SecureBox.Core.DTOs;

namespace SecureBox.Core.Interfaces;

public interface IApiClientService
{
    // Client Management
    Task<IEnumerable<ApiClientDto>> GetAllClientsAsync();
    Task<ApiClientDto?> GetClientByIdAsync(Guid clientId);
    Task<(ApiClientDetailDto client, string clientSecret)> CreateClientAsync(CreateApiClientRequest request, Guid createdBy);
    Task<ApiClientDto> UpdateClientAsync(Guid clientId, UpdateApiClientRequest request);
    Task RevokeClientAsync(Guid clientId, string reason, Guid revokedBy);
    Task DeleteClientAsync(Guid clientId);
    
    // Secret & API Key Management
    Task<ApiClientSecretResponse> RegenerateClientSecretAsync(Guid clientId);
    Task<string> RegenerateApiKeyAsync(Guid clientId);
    
    // OAuth Token Generation
    Task<OAuthTokenResponse> GenerateAccessTokenAsync(string clientId, string clientSecret, string? scope);
    
    // Authentication & Validation
    Task<Guid?> ValidateApiKeyAsync(string apiKey);
    Task<Guid?> ValidateBearerTokenAsync(string token);
    
    // Statistics
    Task<ApiClientStatsDto> GetClientStatsAsync();
    Task LogClientRequestAsync(Guid clientId, string endpoint, string method, int statusCode, string? ipAddress);
}

