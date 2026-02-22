using Vitaent.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vitaent.Api.Auth;
using Vitaent.Api.Tenancy;
using Vitaent.Infrastructure.Persistence;
using Vitaent.Infrastructure.Persistence.Entities;

namespace Vitaent.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/sign-in", SignInAsync).AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        group.MapPost("/sign-out", SignOutAsync).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        ITenantContext tenantContext,
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        RefreshTokenCookieService cookieService,
        HttpContext httpContext)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.NotFound(new { message = "Tenant not found" });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        var tokenService = new TokenService(jwtOptions.Value);
        var accessToken = tokenService.CreateAccessToken(user);

        var rawRefreshToken = TokenService.GenerateRefreshTokenRaw();
        var tokenHash = TokenService.HashRefreshToken(rawRefreshToken);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = cookieService.GetRefreshExpiryUtc().UtcDateTime
        });

        await dbContext.SaveChangesAsync();

        cookieService.Append(httpContext.Response, rawRefreshToken, httpContext.Request.IsHttps);

        return Results.Ok(new SignInResponse(accessToken, new AuthUserDto(user.Id, user.Email, user.Role)));
    }

    private static async Task<IResult> RefreshAsync(
        ITenantContext tenantContext,
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        RefreshTokenCookieService cookieService,
        HttpContext httpContext)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.NotFound(new { message = "Tenant not found" });
        }

        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieService.CookieName, out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
        {
            return Results.Unauthorized();
        }

        var tokenHash = TokenService.HashRefreshToken(rawToken);

        var storedToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (storedToken is null || storedToken.RevokedAt is not null || storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            return Results.Unauthorized();
        }

        if (storedToken.User.TenantId != tenantContext.TenantId)
        {
            return Results.Unauthorized();
        }

        storedToken.RevokedAt = DateTime.UtcNow;

        var rawRefreshToken = TokenService.GenerateRefreshTokenRaw();
        var newHash = TokenService.HashRefreshToken(rawRefreshToken);

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = newHash,
            ExpiresAt = cookieService.GetRefreshExpiryUtc().UtcDateTime
        };

        dbContext.RefreshTokens.Add(replacement);
        await dbContext.SaveChangesAsync();

        var tokenService = new TokenService(jwtOptions.Value);
        var accessToken = tokenService.CreateAccessToken(storedToken.User);

        cookieService.Append(httpContext.Response, rawRefreshToken, httpContext.Request.IsHttps);

        return Results.Ok(new RefreshResponse(accessToken));
    }

    private static async Task<IResult> SignOutAsync(AppDbContext dbContext, HttpContext httpContext, RefreshTokenCookieService cookieService)
    {
        if (httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieService.CookieName, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            var tokenHash = TokenService.HashRefreshToken(rawToken);

            var storedToken = await dbContext.RefreshTokens
                .SingleOrDefaultAsync(x => x.TokenHash == tokenHash);

            if (storedToken is not null && storedToken.RevokedAt is null)
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }

        cookieService.Clear(httpContext.Response, httpContext.Request.IsHttps);
        return Results.NoContent();
    }
}
