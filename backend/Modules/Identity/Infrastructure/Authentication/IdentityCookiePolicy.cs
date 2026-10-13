namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed record IdentityCookiePolicy(bool RequireHttps)
{
    public string RefreshCookieName => RequireHttps
        ? "__Host-ugtm-refresh"
        : "ugtm-refresh";

    public string AntiforgeryCookieName => RequireHttps
        ? "__Host-ugtm-csrf"
        : "ugtm-csrf";
}
