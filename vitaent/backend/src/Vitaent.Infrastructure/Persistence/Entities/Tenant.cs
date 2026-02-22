namespace Vitaent.Infrastructure.Persistence.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public TenantBranding? Branding { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
