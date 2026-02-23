using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vitaent.Application.Tenancy;
using Vitaent.Api.Tenancy;
using Vitaent.Infrastructure.Persistence;
using Vitaent.Infrastructure.Persistence.Seeding;

namespace Vitaent.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext, ITenantContext tenantContext, ITenantSlugResolver slugResolver, DatabaseInitializationState initializationState)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!initializationState.IsReady)
        {
            await WriteProblemAsync(context,
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                initializationState.FailureMessage ?? "Database is initializing. Please retry shortly.");
            return;
        }

        var tenantQuery = context.Request.Query.TryGetValue("tenant", out var tenantValues)
            ? tenantValues.ToString()
            : null;

        var slug = slugResolver.ResolveSlug(context.Request.Host.Value, tenantQuery);

        if (string.IsNullOrWhiteSpace(slug))
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Tenant not found", "Tenant not found");
            return;
        }

        try
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Slug == slug && x.IsActive);

            if (tenant is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Tenant not found", "Tenant not found");
                return;
            }

            context.Items["TenantId"] = tenant.Id;
            context.Items["TenantSlug"] = tenant.Slug;

            tenantContext.TenantId = tenant.Id;
            tenantContext.TenantSlug = tenant.Slug;

            await next(context);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await WriteProblemAsync(context,
                StatusCodes.Status503ServiceUnavailable,
                "Tenant resolution unavailable",
                "Database schema is not initialized yet. Please retry shortly.");
        }

        catch (NpgsqlException)
        {
            await WriteProblemAsync(context,
                StatusCodes.Status503ServiceUnavailable,
                "Tenant resolution unavailable",
                "Database is not available yet. Please retry shortly.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
