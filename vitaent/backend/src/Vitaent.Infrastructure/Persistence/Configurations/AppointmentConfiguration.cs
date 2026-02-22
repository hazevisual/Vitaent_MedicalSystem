using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitaent.Domain.Entities;

namespace Vitaent.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.PatientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StartsAt).IsRequired();
        builder.Property(x => x.EndsAt).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.DoctorId, x.StartsAt }).IsUnique();
    }
}
