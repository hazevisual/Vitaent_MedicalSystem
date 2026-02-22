using Vitaent.Domain.Tenancy;

namespace Vitaent.Domain.Entities;

public class Appointment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DoctorId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }

    public Doctor Doctor { get; set; } = null!;
}
