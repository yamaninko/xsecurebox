using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/oauth")]
public class TokenController : ControllerBase
{
    private readonly IApiClientService _apiClientService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(IApiClientService apiClientService, ILogger<TokenController> logger)
    {
        _apiClientService = apiClientService;
        _logger = logger;
    }

    /// <summary>
    /// OAuth 2.0 Token Endpoint - Client Credentials Grant
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(OAuthTokenResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<OAuthTokenResponse>> GetToken([FromForm] TokenRequest request)
    {
        try
        {
            // Validate grant type
            if (request.grant_type != "client_credentials")
            {
                return BadRequest(new 
                { 
                    error = "unsupported_grant_type",
                    error_description = "Only client_credentials grant type is supported"
                });
            }

            // Validate required parameters
            if (string.IsNullOrWhiteSpace(request.client_id) || string.IsNullOrWhiteSpace(request.client_secret))
            {
                return BadRequest(new 
                { 
                    error = "invalid_request",
                    error_description = "client_id and client_secret are required"
                });
            }

            // Generate access token
            var token = await _apiClientService.GenerateAccessTokenAsync(
                request.client_id,
                request.client_secret,
                request.scope
            );

            return Ok(token);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Invalid client credentials");
            return Unauthorized(new 
            { 
                error = "invalid_client",
                error_description = "Client authentication failed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating access token");
            return StatusCode(500, new 
            { 
                error = "server_error",
                error_description = "An error occurred while processing the request"
            });
        }
    }
}

