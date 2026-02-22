namespace Vitaent.Application.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; set; }
    string TenantSlug { get; set; }
}
