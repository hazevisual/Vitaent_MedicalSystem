using Vitaent.Application.Tenancy;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitaent.Api.Tenancy;
using Vitaent.Infrastructure.Persistence;

namespace Vitaent.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext, ITenantContext tenantContext, ITenantSlugResolver slugResolver)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var tenantQuery = context.Request.Query.TryGetValue("tenant", out var tenantValues)
            ? tenantValues.ToString()
            : null;

        var slug = slugResolver.ResolveSlug(context.Request.Host.Value, tenantQuery);

        if (string.IsNullOrWhiteSpace(slug))
        {
            await WriteTenantNotFoundAsync(context);
            return;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Slug == slug && x.IsActive);

        if (tenant is null)
        {
            await WriteTenantNotFoundAsync(context);
            return;
        }

        context.Items["TenantId"] = tenant.Id;
        context.Items["TenantSlug"] = tenant.Slug;

        tenantContext.TenantId = tenant.Id;
        tenantContext.TenantSlug = tenant.Slug;

        await next(context);
    }

    private static async Task WriteTenantNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Tenant not found",
            Detail = "Tenant not found",
            Type = "https://httpstatuses.com/404"
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
