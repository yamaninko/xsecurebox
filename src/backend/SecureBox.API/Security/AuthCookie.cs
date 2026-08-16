namespace SecureBox.API.Security;

public static class AuthCookie
{
    public const string RefreshName = "sb_refresh";

    public static CookieOptions Build(HttpRequest request, DateTimeOffset expires) =>
        new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires,
            IsEssential = true
        };

    public static void SetRefresh(HttpResponse response, HttpRequest request, string refreshToken, int days)
    {
        response.Cookies.Append(RefreshName, refreshToken, Build(request, DateTimeOffset.UtcNow.AddDays(days)));
    }

    public static void ClearRefresh(HttpResponse response, HttpRequest request)
    {
        response.Cookies.Delete(RefreshName, Build(request, DateTimeOffset.UnixEpoch));
    }
}
