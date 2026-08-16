using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureBox.API.Security;
using SecureBox.Infrastructure.Data;

namespace SecureBox.API.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (context.User.IsAdmin() || context.User.IsApiClient())
        {
            context.Succeed(requirement);
            return;
        }

        Guid userId;
        try
        {
            userId = context.User.GetUserId();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureBoxDbContext>();

        var hasPermission = await db.UserRoles
            .AsNoTracking()
            .AnyAsync(ur =>
                ur.UserId == userId &&
                ur.Role.RolePermissions.Any(rp => rp.Permission.PermissionName == requirement.Permission));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}

public static class PermissionPolicies
{
    public static readonly string[] All =
    {
        "Certificate.Create", "Certificate.Read", "Certificate.Update", "Certificate.Delete",
        "Key.Create", "Key.Read", "Key.Retrieve", "Key.Update", "Key.Delete",
        "User.Create", "User.Read", "User.Update", "User.Delete",
        "Role.Create", "Role.Read", "Role.Update", "Role.Delete",
        "Audit.Read"
    };
}
