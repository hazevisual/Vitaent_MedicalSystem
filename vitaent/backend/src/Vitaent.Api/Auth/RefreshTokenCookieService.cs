using Microsoft.Extensions.Options;

namespace Vitaent.Api.Auth;

public class RefreshTokenCookieService(IOptions<JwtOptions> jwtOptions)
{
    public const string CookieName = "vitaent_refresh";

    public void Append(HttpResponse response, string rawToken, bool isHttps)
    {
        response.Cookies.Append(CookieName, rawToken, BuildOptions(isHttps));
    }

    public void Clear(HttpResponse response, bool isHttps)
    {
        response.Cookies.Delete(CookieName, BuildOptions(isHttps));
    }

    public DateTimeOffset GetRefreshExpiryUtc()
    {
        return DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);
    }

    private CookieOptions BuildOptions(bool isHttps)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = isHttps,
            Expires = GetRefreshExpiryUtc(),
            Path = "/"
        };
    }
}
