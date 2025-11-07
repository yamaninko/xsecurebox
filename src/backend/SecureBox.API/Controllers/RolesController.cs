using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RolesController> _logger;
    
    public RolesController(IRoleService roleService, ILogger<RolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles()
    {
        try
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(new { success = true, data = roles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get roles failed");
            return StatusCode(500, new { success = false, error = new { code = "GET_ROLES_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpGet("{roleId:guid}")]
    public async Task<ActionResult<RoleDto>> GetRole(Guid roleId)
    {
        try
        {
            var role = await _roleService.GetRoleByIdAsync(roleId);
            if (role == null)
                return NotFound(new { success = false, error = new { code = "ROLE_NOT_FOUND", message = "Role not found" } });
            
            return Ok(new { success = true, data = role });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get role failed for roleId: {RoleId}", roleId);
            return StatusCode(500, new { success = false, error = new { code = "GET_ROLE_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpPost]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var role = await _roleService.CreateRoleAsync(request, userId);
            return CreatedAtAction(nameof(GetRole), new { roleId = role.RoleId }, 
                new { success = true, data = role, message = "Role created successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "ROLE_EXISTS", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create role failed");
            return StatusCode(500, new { success = false, error = new { code = "CREATE_ROLE_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpPut("{roleId:guid}")]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid roleId, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var role = await _roleService.UpdateRoleAsync(roleId, request);
            return Ok(new { success = true, data = role, message = "Role updated successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, error = new { code = "ROLE_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "CANNOT_MODIFY_SYSTEM_ROLE", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update role failed for roleId: {RoleId}", roleId);
            return StatusCode(500, new { success = false, error = new { code = "UPDATE_ROLE_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpDelete("{roleId:guid}")]
    public async Task<ActionResult> DeleteRole(Guid roleId)
    {
        try
        {
            await _roleService.DeleteRoleAsync(roleId);
            return Ok(new { success = true, message = "Role deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, error = new { code = "ROLE_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "CANNOT_DELETE_SYSTEM_ROLE", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete role failed for roleId: {RoleId}", roleId);
            return StatusCode(500, new { success = false, error = new { code = "DELETE_ROLE_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpGet("permissions")]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissions()
    {
        try
        {
            var permissions = await _roleService.GetPermissionsAsync();
            return Ok(new { success = true, data = permissions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get permissions failed");
            return StatusCode(500, new { success = false, error = new { code = "GET_PERMISSIONS_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpGet("{roleId:guid}/permissions")]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetRolePermissions(Guid roleId)
    {
        try
        {
            var permissions = await _roleService.GetRolePermissionsAsync(roleId);
            return Ok(new { success = true, data = permissions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get role permissions failed for roleId: {RoleId}", roleId);
            return StatusCode(500, new { success = false, error = new { code = "GET_ROLE_PERMISSIONS_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpPost("{roleId:guid}/permissions/{permissionId:guid}")]
    public async Task<ActionResult> AssignPermission(Guid roleId, Guid permissionId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            await _roleService.AssignPermissionToRoleAsync(roleId, permissionId, userId);
            return Ok(new { success = true, message = "Permission assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assign permission failed");
            return StatusCode(500, new { success = false, error = new { code = "ASSIGN_PERMISSION_ERROR", message = "An error occurred" } });
        }
    }
    
    [HttpDelete("{roleId:guid}/permissions/{permissionId:guid}")]
    public async Task<ActionResult> RemovePermission(Guid roleId, Guid permissionId)
    {
        try
        {
            await _roleService.RemovePermissionFromRoleAsync(roleId, permissionId);
            return Ok(new { success = true, message = "Permission removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remove permission failed");
            return StatusCode(500, new { success = false, error = new { code = "REMOVE_PERMISSION_ERROR", message = "An error occurred" } });
        }
    }
}

