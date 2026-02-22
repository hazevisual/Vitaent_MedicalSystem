using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitaent.Infrastructure.Persistence.Entities;

namespace Vitaent.Infrastructure.Persistence.Configurations;

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_branding");

        builder.HasKey(x => x.TenantId);
        builder.Property(x => x.Json).HasColumnType("jsonb").IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithOne(x => x.Branding)
            .HasForeignKey<TenantBranding>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
