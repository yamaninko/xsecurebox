using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SecureBox.API.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Kullanıcı doğrulanamadı");
        }

        return userId;
    }

    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole("Admin");

    public static bool IsApiClient(this ClaimsPrincipal user) =>
        !string.IsNullOrWhiteSpace(user.FindFirstValue("client_id"));

    public static bool HasScope(this ClaimsPrincipal user, params string[] required)
    {
        if (!user.IsApiClient())
        {
            return true;
        }

        var granted = user.FindAll("scope").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return required.Any(granted.Contains);
    }

    public static string? GetTokenId(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Jti);
}
