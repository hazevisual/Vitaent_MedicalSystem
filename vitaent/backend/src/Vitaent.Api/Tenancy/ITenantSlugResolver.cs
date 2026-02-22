namespace Vitaent.Api.Tenancy;

public interface ITenantSlugResolver
{
    string? ResolveSlug(string? host, string? tenantQuery);
}
