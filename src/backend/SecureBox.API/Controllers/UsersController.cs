using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;
    
    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }
    
    /// <summary>
    /// List all users (Admin only)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] UserQueryParams queryParams)
    {
        try
        {
            var users = await _userService.GetAllUsersAsync(queryParams);
            return Ok(new { success = true, data = users });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get users failed");
            return StatusCode(500, new { success = false, error = new { code = "GET_USERS_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(userId);
            
            if (user == null)
                return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found" } });
            
            return Ok(new { success = true, data = user });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get user failed for userId: {UserId}", userId);
            return StatusCode(500, new { success = false, error = new { code = "GET_USER_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Create new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(request);
            
            return CreatedAtAction(nameof(GetUser), new { userId = user.UserId }, 
                new { success = true, data = user, message = "User created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create user failed");
            return StatusCode(500, new { success = false, error = new { code = "CREATE_USER_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("{userId:guid}")]
    public async Task<ActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            await _userService.UpdateUserAsync(userId, request);
            return Ok(new { success = true, message = "User updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update user failed for userId: {UserId}", userId);
            return StatusCode(500, new { success = false, error = new { code = "UPDATE_USER_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Delete user (soft delete)
    /// </summary>
    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult> DeleteUser(Guid userId)
    {
        try
        {
            await _userService.DeleteUserAsync(userId);
            return Ok(new { success = true, message = "User deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete user failed for userId: {UserId}", userId);
            return StatusCode(500, new { success = false, error = new { code = "DELETE_USER_ERROR", message = "An error occurred" } });
        }
    }
}

