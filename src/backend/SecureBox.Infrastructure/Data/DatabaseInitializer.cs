using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureBox.Core.Entities;

namespace SecureBox.Infrastructure.Data;

public static class DatabaseInitializer
{
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly PermissionSeed[] PermissionSeeds =
    {
        new("Certificate.Create", "Certificate", "Create", "Upload/create new certificates"),
        new("Certificate.Read", "Certificate", "Read", "View certificate details"),
        new("Certificate.Update", "Certificate", "Update", "Update certificate metadata"),
        new("Certificate.Delete", "Certificate", "Delete", "Delete/revoke certificates"),
        new("Key.Create", "Key", "Create", "Create new keys"),
        new("Key.Read", "Key", "Read", "View key metadata"),
        new("Key.Retrieve", "Key", "Retrieve", "Retrieve decrypted key value"),
        new("Key.Update", "Key", "Update", "Update key metadata"),
        new("Key.Delete", "Key", "Delete", "Delete keys"),
        new("User.Create", "User", "Create", "Create new users"),
        new("User.Read", "User", "Read", "View user details"),
        new("User.Update", "User", "Update", "Update user information"),
        new("User.Delete", "User", "Delete", "Delete users"),
        new("Role.Create", "Role", "Create", "Create new roles"),
        new("Role.Read", "Role", "Read", "View role details"),
        new("Role.Update", "Role", "Update", "Update role permissions"),
        new("Role.Delete", "Role", "Delete", "Delete roles"),
        new("Audit.Read", "Audit", "Read", "View audit logs")
    };

    private static readonly RoleSeed[] RoleSeeds =
    {
        new(
            "Admin",
            "Full system access",
            true,
            PermissionSeeds.Select(p => p.PermissionName).ToArray()),
        new(
            "Client",
            "Client user with key access",
            true,
            new[]
            {
                "Key.Read",
                "Key.Retrieve",
                "Key.Create",
                "Certificate.Read",
                "User.Read"
            }),
        new(
            "Service",
            "Service account for API integration",
            true,
            new[]
            {
                "Key.Retrieve",
                "Key.Read",
                "Certificate.Read"
            })
    };

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitializer");
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureBoxDbContext>();

        var options = configuration.GetSection("Database").Get<DatabaseOptions>() ?? DatabaseOptions.Default;

        if (!options.ApplyMigrationsOnStartup && !options.SeedDefaultsOnStartup)
        {
            logger.LogInformation("Database initializer disabled via configuration.");
            return;
        }

        if (options.ApplyMigrationsOnStartup && dbContext.Database.IsRelational())
        {
            await ApplyMigrationsAsync(dbContext, logger, cancellationToken);
        }

        await EnsureMfaColumnsAsync(dbContext, logger, cancellationToken);
        await EnsureChainColumnsAsync(dbContext, logger, cancellationToken);

        if (options.SeedDefaultsOnStartup)
        {
            await SeedDefaultsAsync(dbContext, configuration, logger, cancellationToken);
        }
    }

    private static async Task EnsureMfaColumnsAsync(
        SecureBoxDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "MfaEnabled" BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "MustSetupMfa" BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "TotpSecretProtected" BYTEA;
            UPDATE "Users" SET "MustSetupMfa" = TRUE WHERE "Username" = 'admin' AND "MfaEnabled" = FALSE;
            """,
            cancellationToken);
        logger.LogInformation("Ensured MFA columns on Users.");
    }

    private static async Task EnsureChainColumnsAsync(
        SecureBoxDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Keys" ADD COLUMN IF NOT EXISTS "ChainPayloadHash" VARCHAR(80);
            ALTER TABLE "Keys" ADD COLUMN IF NOT EXISTS "ChainTxHash" VARCHAR(80);
            ALTER TABLE "Keys" ADD COLUMN IF NOT EXISTS "ChainBlockNumber" BIGINT;
            CREATE TABLE IF NOT EXISTS "ChainState" (
                "Id" INT PRIMARY KEY,
                "ContractAddress" VARCHAR(64),
                "SystemId" VARCHAR(80),
                "DeployTxHash" VARCHAR(80),
                "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            cancellationToken);
        logger.LogInformation("Ensured Ethereum commitment columns.");
    }

    private static async Task ApplyMigrationsAsync(
        SecureBoxDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying database migrations (if any).");
        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrations applied.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
        {
            logger.LogWarning(ex, "Schema already exists (likely created by init.sql). Continuing without EF migrate.");
        }
    }

    private static async Task SeedDefaultsAsync(
        SecureBoxDbContext dbContext,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var permissions = await EnsurePermissionsAsync(dbContext, now, cancellationToken);
        var roles = await EnsureRolesAsync(dbContext, now, cancellationToken);
        await EnsureRolePermissionsAsync(dbContext, roles, permissions, now, cancellationToken);
        await EnsureAdminUserAsync(dbContext, roles, configuration, logger, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Default Secure Box data ensured.");
    }

    private static async Task<Dictionary<string, Permission>> EnsurePermissionsAsync(
        SecureBoxDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var allPermissions = await dbContext.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var existing = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);
        foreach (var permission in allPermissions)
        {
            existing.TryAdd(permission.PermissionName, permission);
        }

        foreach (var seed in PermissionSeeds)
        {
            if (existing.ContainsKey(seed.PermissionName))
            {
                continue;
            }

            var permission = new Permission
            {
                PermissionId = Guid.NewGuid(),
                PermissionName = seed.PermissionName,
                Resource = seed.Resource,
                Action = seed.Action,
                Description = seed.Description,
                CreatedAt = now
            };

            dbContext.Permissions.Add(permission);
            existing[seed.PermissionName] = permission;
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                // Ignore duplicate key errors - another instance may have seeded the data
                // Reload the permissions from database
                dbContext.ChangeTracker.Clear();
                var reloadedPermissions = await dbContext.Permissions
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                existing.Clear();
                foreach (var permission in reloadedPermissions)
                {
                    existing.TryAdd(permission.PermissionName, permission);
                }
            }
        }

        return existing;
    }

    private static async Task<Dictionary<string, Role>> EnsureRolesAsync(
        SecureBoxDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var allRoles = await dbContext.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var existing = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in allRoles)
        {
            existing.TryAdd(role.RoleName, role);
        }

        foreach (var seed in RoleSeeds)
        {
            if (existing.ContainsKey(seed.RoleName))
            {
                continue;
            }

            var role = new Role
            {
                RoleId = Guid.NewGuid(),
                RoleName = seed.RoleName,
                Description = seed.Description,
                IsSystem = seed.IsSystem,
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.Roles.Add(role);
            existing[seed.RoleName] = role;
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                // Ignore duplicate key errors - another instance may have seeded the data
                // Reload the roles from database
                dbContext.ChangeTracker.Clear();
                var reloadedRoles = await dbContext.Roles
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                existing.Clear();
                foreach (var role in reloadedRoles)
                {
                    existing.TryAdd(role.RoleName, role);
                }
            }
        }

        return existing;
    }

    private static async Task EnsureRolePermissionsAsync(
        SecureBoxDbContext dbContext,
        IReadOnlyDictionary<string, Role> roles,
        IReadOnlyDictionary<string, Permission> permissions,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var seed in RoleSeeds)
        {
            if (!roles.TryGetValue(seed.RoleName, out var role))
            {
                continue;
            }

            var permissionIds = seed.PermissionNames
                .Where(permissions.ContainsKey)
                .Select(name => permissions[name].PermissionId)
                .ToList();

            var existingRolePermissions = await dbContext.RolePermissions
                .Where(rp => rp.RoleId == role.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken);

            var missingPermissions = permissionIds
                .Where(id => !existingRolePermissions.Contains(id))
                .ToList();

            foreach (var permissionId in missingPermissions)
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RolePermissionId = Guid.NewGuid(),
                    RoleId = role.RoleId,
                    PermissionId = permissionId,
                    GrantedAt = now
                });
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                // Ignore duplicate key errors - another instance may have seeded the data
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private static async Task EnsureAdminUserAsync(
        SecureBoxDbContext dbContext,
        IReadOnlyDictionary<string, Role> roles,
        IConfiguration configuration,
        ILogger logger,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var adminUser = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == "admin", cancellationToken);

        if (adminUser is null)
        {
            var defaultPassword = configuration["Database:DefaultAdminPassword"];
            if (string.IsNullOrWhiteSpace(defaultPassword))
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (string.Equals(env, "Testing", StringComparison.OrdinalIgnoreCase))
                {
                    defaultPassword = "Admin@123";
                }
                else
                {
                    throw new InvalidOperationException("Database__DefaultAdminPassword / ADMIN_PASSWORD is required to seed admin");
                }
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword, 12);

            adminUser = new User
            {
                UserId = AdminUserId,
                Username = "admin",
                Email = "admin@securebox.local",
                PasswordHash = passwordHash,
                FirstName = "System",
                LastName = "Administrator",
                IsActive = true,
                IsEmailVerified = true,
                MustChangePassword = true,
                MustSetupMfa = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.Users.Add(adminUser);
        }

        if (roles.TryGetValue("Admin", out var adminRole))
        {
            var hasAdminRole = await dbContext.UserRoles
                .AnyAsync(ur => ur.UserId == adminUser.UserId && ur.RoleId == adminRole.RoleId, cancellationToken);

            if (!hasAdminRole)
            {
                dbContext.UserRoles.Add(new UserRole
                {
                    UserRoleId = Guid.NewGuid(),
                    UserId = adminUser.UserId,
                    RoleId = adminRole.RoleId,
                    AssignedAt = now,
                    AssignedBy = adminUser.UserId
                });
            }
        }
    }

    private sealed record PermissionSeed(
        string PermissionName,
        string Resource,
        string Action,
        string Description);

    private sealed record RoleSeed(
        string RoleName,
        string Description,
        bool IsSystem,
        string[] PermissionNames);

    private sealed record DatabaseOptions(
        bool ApplyMigrationsOnStartup,
        bool SeedDefaultsOnStartup)
    {
        public static DatabaseOptions Default => new(true, true);
    }
}
