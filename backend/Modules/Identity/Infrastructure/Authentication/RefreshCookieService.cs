using Microsoft.AspNetCore.Http;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class RefreshCookieService(IdentityCookiePolicy cookiePolicy)
{
    public string? Read(HttpRequest request)
        => request.Cookies[cookiePolicy.RefreshCookieName];

    public void Write(HttpResponse response, string token, DateTime expiresAt)
    {
        response.Cookies.Append(cookiePolicy.RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = cookiePolicy.RequireHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = new DateTimeOffset(expiresAt)
        });
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(cookiePolicy.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = cookiePolicy.RequireHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }
}
