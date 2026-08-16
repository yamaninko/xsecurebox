using System;
using System.Collections.Generic;

namespace SecureBox.Core.Entities;

public class ApiClient
{
    public Guid ClientId { get; set; } = Guid.NewGuid();
    
    // Client Identification
    public required string ClientName { get; set; }
    public string? Description { get; set; }
    public required string ClientIdString { get; set; } // Human-readable client ID
    public required string ClientSecretHash { get; set; } // Hashed secret
    public required string ApiKey { get; set; } // For simple API key authentication
    
    // Client Configuration
    public required string Scopes { get; set; } // JSON array: ["keys:read", "keys:write", "certs:read"]
    public int RateLimitPerMinute { get; set; } = 60;
    public int RateLimitPerHour { get; set; } = 1000;
    
    // Status
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    public long TotalRequests { get; set; } = 0;
    
    // Audit
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevokedReason { get; set; }
    
    // Navigation
    public User? Creator { get; set; }
    public ICollection<ApiClientRequest> Requests { get; set; } = new List<ApiClientRequest>();
}

public class ApiClientRequest
{
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public required string Endpoint { get; set; }
    public required string Method { get; set; }
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ApiClient? Client { get; set; }
}

