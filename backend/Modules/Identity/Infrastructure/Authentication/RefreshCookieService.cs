using Microsoft.AspNetCore.Http;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class RefreshCookieService
{
    public const string CookieName = "__Host-ugtm-refresh";

    public string? Read(HttpRequest request)
        => request.Cookies[CookieName];

    public void Write(HttpResponse response, string token, DateTime expiresAt)
    {
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = new DateTimeOffset(expiresAt)
        });
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }
}