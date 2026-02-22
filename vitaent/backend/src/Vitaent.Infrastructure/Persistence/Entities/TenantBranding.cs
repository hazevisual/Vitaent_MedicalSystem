namespace Vitaent.Infrastructure.Persistence.Entities;

public class TenantBranding
{
    public Guid TenantId { get; set; }
    public string Json { get; set; } = "{}";

    public Tenant Tenant { get; set; } = null!;
}
