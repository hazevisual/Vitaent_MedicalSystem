using Vitaent.Domain.Tenancy;

namespace Vitaent.Domain.Entities;

public class Doctor : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
