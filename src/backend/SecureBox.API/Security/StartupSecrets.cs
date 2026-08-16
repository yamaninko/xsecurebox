using SecureBox.Infrastructure.Services;

namespace SecureBox.API.Security;

public static class StartupSecrets
{
    public static readonly string[] ForbiddenAdminPasswords =
    {
        "Admin@123",
        "Admin@1231",
        "admin",
        "password",
        "Password1!"
    };

    public const string DevJwt = "YourSuperSecretKeyMinimum32CharactersLongForHS256!";

    public static void Validate(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var jwt = configuration["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwt) || string.Equals(jwt, DevJwt, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Production JWT secret must be overridden via JwtSettings__SecretKey");
        }

        var kek = configuration["Encryption:KeyEncryptionKey"];
        if (string.IsNullOrWhiteSpace(kek) || string.Equals(kek, EncryptionService.DevKek, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Production Encryption:KeyEncryptionKey must be a unique 32-byte secret");
        }

        var admin = configuration["Database:DefaultAdminPassword"];
        if (!string.IsNullOrWhiteSpace(admin) &&
            ForbiddenAdminPasswords.Contains(admin, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Production Database__DefaultAdminPassword cannot be a known default");
        }
    }
}
