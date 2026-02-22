using Vitaent.Application.Tenancy;

namespace Vitaent.Api.Tenancy;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; } = Guid.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}
