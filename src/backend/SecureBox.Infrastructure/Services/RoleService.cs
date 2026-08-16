using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly SecureBoxDbContext _dbContext;
    private readonly ILogger<RoleService> _logger;

    public RoleService(SecureBoxDbContext dbContext, ILogger<RoleService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _dbContext.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .Select(r => new RoleDto(
                r.RoleId,
                r.RoleName,
                r.Description,
                r.IsSystem,
                r.UserRoles.Count,
                r.RolePermissions.Count,
                r.CreatedAt
            ))
            .ToListAsync();

        return roles;
    }

    public async Task<RoleDto?> GetRoleByIdAsync(Guid roleId)
    {
        var role = await _dbContext.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .Where(r => r.RoleId == roleId)
            .Select(r => new RoleDto(
                r.RoleId,
                r.RoleName,
                r.Description,
                r.IsSystem,
                r.UserRoles.Count,
                r.RolePermissions.Count,
                r.CreatedAt
            ))
            .FirstOrDefaultAsync();

        return role;
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, Guid createdBy)
    {
        var exists = await _dbContext.Roles.AnyAsync(r => r.RoleName == request.RoleName);
        if (exists)
            throw new InvalidOperationException($"Role '{request.RoleName}' already exists");

        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            RoleName = request.RoleName,
            Description = request.Description,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Roles.Add(role);

        // Assign permissions if provided
        if (request.PermissionIds != null && request.PermissionIds.Any())
        {
            foreach (var permissionId in request.PermissionIds)
            {
                _dbContext.RolePermissions.Add(new RolePermission
                {
                    RolePermissionId = Guid.NewGuid(),
                    RoleId = role.RoleId,
                    PermissionId = permissionId,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = createdBy
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        return new RoleDto(
            role.RoleId,
            role.RoleName,
            role.Description,
            role.IsSystem,
            0,
            request.PermissionIds?.Count ?? 0,
            role.CreatedAt
        );
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request)
    {
        var role = await _dbContext.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null)
            throw new KeyNotFoundException($"Role with ID {roleId} not found");

        if (role.IsSystem)
            throw new InvalidOperationException("Cannot modify system roles");

        if (request.RoleName != null)
            role.RoleName = request.RoleName;

        if (request.Description != null)
            role.Description = request.Description;

        role.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return new RoleDto(
            role.RoleId,
            role.RoleName,
            role.Description,
            role.IsSystem,
            role.UserRoles.Count,
            role.RolePermissions.Count,
            role.CreatedAt
        );
    }

    public async Task DeleteRoleAsync(Guid roleId)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null)
            throw new KeyNotFoundException($"Role with ID {roleId} not found");

        if (role.IsSystem)
            throw new InvalidOperationException("Cannot delete system roles");

        role.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, Guid grantedBy)
    {
        var exists = await _dbContext.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (exists)
            return;

        _dbContext.RolePermissions.Add(new RolePermission
        {
            RolePermissionId = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = grantedBy
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId)
    {
        var rolePermission = await _dbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (rolePermission != null)
        {
            _dbContext.RolePermissions.Remove(rolePermission);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<PermissionDto>> GetPermissionsAsync()
    {
        var permissions = await _dbContext.Permissions
            .Select(p => new PermissionDto(
                p.PermissionId,
                p.PermissionName,
                p.Resource,
                p.Action,
                p.Description
            ))
            .ToListAsync();

        return permissions;
    }

    public async Task<IEnumerable<PermissionDto>> GetRolePermissionsAsync(Guid roleId)
    {
        var permissions = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Include(rp => rp.Permission)
            .Select(rp => new PermissionDto(
                rp.Permission.PermissionId,
                rp.Permission.PermissionName,
                rp.Permission.Resource,
                rp.Permission.Action,
                rp.Permission.Description
            ))
            .ToListAsync();

        return permissions;
    }
}

