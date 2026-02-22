namespace Vitaent.Api.Tenancy;

public class TenantSlugResolver : ITenantSlugResolver
{
    public string? ResolveSlug(string? host, string? tenantQuery)
    {
        if (!string.IsNullOrWhiteSpace(tenantQuery))
        {
            return tenantQuery.Trim().ToLowerInvariant();
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var hostWithoutPort = host.Split(':', 2)[0];
        var labels = hostWithoutPort.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (labels.Length <= 1)
        {
            return null;
        }

        return labels[0].ToLowerInvariant();
    }
}
