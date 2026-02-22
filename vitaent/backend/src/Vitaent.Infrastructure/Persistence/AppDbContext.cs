using Microsoft.EntityFrameworkCore;
using Vitaent.Application.Tenancy;
using Vitaent.Domain.Entities;
using Vitaent.Infrastructure.Persistence.Entities;

namespace Vitaent.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<TenantBranding>()
            .HasQueryFilter(entity => entity.TenantId == CurrentTenantId);

        modelBuilder.Entity<User>()
            .HasQueryFilter(entity => entity.TenantId == CurrentTenantId);

        modelBuilder.Entity<RefreshToken>()
            .HasQueryFilter(entity => entity.User.TenantId == CurrentTenantId);

        modelBuilder.Entity<Doctor>()
            .HasQueryFilter(entity => entity.TenantId == CurrentTenantId);

        modelBuilder.Entity<Appointment>()
            .HasQueryFilter(entity => entity.TenantId == CurrentTenantId);

        base.OnModelCreating(modelBuilder);
    }

    private Guid CurrentTenantId => _tenantContext?.TenantId ?? Guid.Empty;
}
