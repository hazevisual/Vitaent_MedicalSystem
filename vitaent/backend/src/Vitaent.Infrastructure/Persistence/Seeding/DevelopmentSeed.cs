using Microsoft.EntityFrameworkCore;
using Vitaent.Domain.Entities;
using Vitaent.Infrastructure.Persistence.Entities;

namespace Vitaent.Infrastructure.Persistence.Seeding;

public static class DevelopmentSeed
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Slug == "clinic1", cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Slug = "clinic1",
                Name = "Clinic 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var brandingExists = await dbContext.TenantBrandings
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant.Id, cancellationToken);

        if (!brandingExists)
        {
            dbContext.TenantBrandings.Add(new TenantBranding
            {
                TenantId = tenant.Id,
                Json = "{\"theme\":\"light\",\"primaryColor\":\"#0ea5e9\",\"logoUrl\":null}"
            });
        }

        var adminExists = await dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant.Id && x.Email == "admin@clinic1.local", cancellationToken);

        if (!adminExists)
        {
            dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Email = "admin@clinic1.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = "Owner"
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var doctorExists = await dbContext.Doctors
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant.Id && x.Name == "Dr. Strange", cancellationToken);

        if (!doctorExists)
        {
            dbContext.Doctors.Add(new Doctor
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Dr. Strange",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
