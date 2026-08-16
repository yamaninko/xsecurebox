using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class UserService : IUserService
{
    public const int PasswordWorkFactor = 12;

    private readonly SecureBoxDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(SecureBoxDbContext dbContext, ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(UserQueryParams queryParams)
    {
        var page = Math.Max(1, queryParams.Page);
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);

        var query = _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var term = queryParams.Search.Trim();
            query = query.Where(u => u.Username.Contains(term) || u.Email.Contains(term));
        }

        if (queryParams.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == queryParams.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Role))
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == queryParams.Role));
        }

        var users = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return users.Select(Map);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await LoadUserAsync(userId);
        return user is null ? null : Map(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        if (await _dbContext.Users.AnyAsync(u => u.Username == request.Username))
        {
            throw new InvalidOperationException("Username is already taken");
        }

        if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("Email is already taken");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, PasswordWorkFactor),
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);

        var roleIds = request.RoleIds ?? new List<Guid>();
        if (roleIds.Count == 0)
        {
            var clientRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");
            if (clientRole != null)
            {
                roleIds.Add(clientRole.RoleId);
            }
        }

        foreach (var roleId in roleIds.Distinct())
        {
            if (!await _dbContext.Roles.AnyAsync(r => r.RoleId == roleId))
            {
                throw new InvalidOperationException($"Role {roleId} was not found");
            }

            _dbContext.UserRoles.Add(new UserRole
            {
                UserRoleId = Guid.NewGuid(),
                UserId = user.UserId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created user {Username}", user.Username);
        var created = await LoadUserAsync(user.UserId);
        return Map(created!);
    }

    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        var user = await LoadUserAsync(userId) ?? throw new KeyNotFoundException("User not found");

        if (request.Email != null)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email && u.UserId != userId))
            {
                throw new InvalidOperationException("Email is already taken");
            }

            user.Email = request.Email;
        }

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Map(user);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await LoadUserAsync(userId) ?? throw new KeyNotFoundException("User not found");

        var isLastAdmin = user.UserRoles.Any(ur => ur.Role.RoleName == "Admin") &&
                          await _dbContext.UserRoles.CountAsync(ur => ur.Role.RoleName == "Admin") <= 1;
        if (isLastAdmin)
        {
            throw new InvalidOperationException("Cannot delete the last admin user");
        }

        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId, Guid assignedBy)
    {
        if (!await _dbContext.Users.AnyAsync(u => u.UserId == userId))
        {
            throw new KeyNotFoundException("User not found");
        }

        if (!await _dbContext.Roles.AnyAsync(r => r.RoleId == roleId))
        {
            throw new KeyNotFoundException("Role not found");
        }

        var exists = await _dbContext.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (exists)
        {
            return;
        }

        _dbContext.UserRoles.Add(new UserRole
        {
            UserRoleId = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId)
    {
        var userRole = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole is null)
        {
            return;
        }

        if (userRole.Role.RoleName == "Admin")
        {
            var adminCount = await _dbContext.UserRoles.CountAsync(ur => ur.Role.RoleName == "Admin");
            if (adminCount <= 1)
            {
                throw new InvalidOperationException("Cannot remove the last admin role");
            }
        }

        _dbContext.UserRoles.Remove(userRole);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<User?> LoadUserAsync(Guid userId)
    {
        return await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    internal static UserDto Map(User user)
    {
        var roles = user.UserRoles
            .Select(ur => ur.Role?.RoleName)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!)
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role?.RolePermissions != null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.PermissionName)
            .Distinct()
            .ToList();

        return new UserDto(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            roles,
            permissions,
            user.MustChangePassword,
            user.MfaEnabled,
            user.MustSetupMfa,
            user.CreatedAt,
            user.LastLoginAt);
    }
}
