using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.API.Security;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/v1/clients")]
public class ApiClientsController : ControllerBase
{
    private readonly IApiClientService _apiClientService;
    private readonly ILogger<ApiClientsController> _logger;

    public ApiClientsController(IApiClientService apiClientService, ILogger<ApiClientsController> logger)
    {
        _apiClientService = apiClientService;
        _logger = logger;
    }

    private Guid GetCurrentUserId() => User.GetUserId();

    /// <summary>
    /// Get all API clients
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAllClients()
    {
        try
        {
            var clients = await _apiClientService.GetAllClientsAsync();
            return Ok(new { success = true, data = clients });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API clients");
            return StatusCode(500, new { success = false, error = new { code = "GET_CLIENTS_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Get API client by ID
    /// </summary>
    [HttpGet("{clientId}")]
    public async Task<ActionResult> GetClient(Guid clientId)
    {
        try
        {
            var client = await _apiClientService.GetClientByIdAsync(clientId);
            if (client == null)
                return NotFound(new { success = false, error = new { code = "CLIENT_NOT_FOUND", message = "Client not found" } });

            return Ok(new { success = true, data = client });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API client {ClientId}", clientId);
            return StatusCode(500, new { success = false, error = new { code = "GET_CLIENT_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Create a new API client
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> CreateClient([FromBody] CreateApiClientRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var (client, clientSecret) = await _apiClientService.CreateClientAsync(request, userId);

            return Ok(new 
            { 
                success = true, 
                data = new 
                { 
                    client,
                    clientSecret, // Only shown once!
                    message = "IMPORTANT: Save the client secret securely. It will not be shown again!"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API client");
            return StatusCode(500, new { success = false, error = new { code = "CREATE_CLIENT_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Update API client
    /// </summary>
    [HttpPut("{clientId}")]
    public async Task<ActionResult> UpdateClient(Guid clientId, [FromBody] UpdateApiClientRequest request)
    {
        try
        {
            var client = await _apiClientService.UpdateClientAsync(clientId, request);
            return Ok(new { success = true, data = client });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, error = new { code = "CLIENT_NOT_FOUND", message = "Client not found" } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API client {ClientId}", clientId);
            return StatusCode(500, new { success = false, error = new { code = "UPDATE_CLIENT_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Revoke API client
    /// </summary>
    [HttpPost("{clientId}/revoke")]
    public async Task<ActionResult> RevokeClient(Guid clientId, [FromBody] RevokeClientRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _apiClientService.RevokeClientAsync(clientId, request.Reason, userId);
            return Ok(new { success = true, message = "Client revoked successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, error = new { code = "CLIENT_NOT_FOUND", message = "Client not found" } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API client {ClientId}", clientId);
            return StatusCode(500, new { success = false, error = new { code = "REVOKE_CLIENT_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Delete API client
    /// </summary>
    [HttpDelete("{clientId}")]
    public async Task<ActionResult> DeleteClient(Guid clientId)
    {
        try
        {
            await _apiClientService.DeleteClientAsync(clientId);
            return Ok(new { success = true, message = "Client deleted successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, error = new { code = "CLIENT_NOT_FOUND", message = "Client not found" } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API client {ClientId}", clientId);
            return StatusCode(500, new { success = false, error = new { code = "DELETE_CLIENT_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Regenerate client secret
    /// </summary>
    [HttpPost("{clientId}/regenerate-secret")]
    public async Task<ActionResult> RegenerateSecret(Guid clientId)
    {
        try
        {
            var response = await _apiClientService.RegenerateClientSecretAsync(clientId);
            return Ok(new 
            { 
                success = true, 
                data = new 
                { 
                    response.ClientId,
                    response.ClientSecret,
                    message = "IMPORTANT: Save the new client secret securely. It will not be shown again!"
                }
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, error = new { code = "CLIENT_NOT_FOUND", message = "Client not found" } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating client secret {ClientId}", clientId);
            return StatusCode(500, new { success = false, error = new { code = "REGENERATE_SECRET_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Regenerate API key
    /// </summary>
    [HttpPost("{clientId}/regenerate-apikey")]
    public async Task<ActionResult> RegenerateApiKey(Guid clientId)
    {
        try
        {
            var apiKey = await _apiClientService.RegenerateApiKeyAsync(clientId);
            return Ok(new 
            { 
                success = true, 
                data = new 
                { 
                    apiKey,
                    message = "API key regenerated successfully"
                }
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, error = new { code = "CLIENT_NOT_FOUND", message = "Client not found" } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating API key {ClientId}", clientId);
            return StatusCode(500, new { success = false, error = new { code = "REGENERATE_APIKEY_ERROR", message = "An error occurred" } });
        }
    }

    /// <summary>
    /// Get API client statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        try
        {
            var stats = await _apiClientService.GetClientStatsAsync();
            return Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API client stats");
            return StatusCode(500, new { success = false, error = new { code = "GET_STATS_ERROR", message = "An error occurred" } });
        }
    }
}

public record RevokeClientRequest(string Reason);

