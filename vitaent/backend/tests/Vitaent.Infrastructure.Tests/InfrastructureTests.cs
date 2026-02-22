using Microsoft.EntityFrameworkCore;
using Vitaent.Application.Tenancy;
using Vitaent.Infrastructure.Persistence;
using Vitaent.Infrastructure.Persistence.Entities;

namespace Vitaent.Infrastructure.Tests;

public class InfrastructureTests
{
    [Fact]
    public async Task GlobalTenantFilters_ReturnOnlyCurrentTenantData()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant-filter-{Guid.NewGuid()}")
            .Options;

        await using (var seedContext = new AppDbContext(options, new TestTenantContext { TenantId = tenantA }))
        {
            seedContext.Users.AddRange(
                new User { Id = Guid.NewGuid(), TenantId = tenantA, Email = "a@clinic.local", PasswordHash = "hash", Role = "Owner" },
                new User { Id = Guid.NewGuid(), TenantId = tenantB, Email = "b@clinic.local", PasswordHash = "hash", Role = "Owner" });

            seedContext.TenantBrandings.AddRange(
                new TenantBranding { TenantId = tenantA, Json = "{}" },
                new TenantBranding { TenantId = tenantB, Json = "{}" });

            await seedContext.SaveChangesAsync();
        }

        await using (var tenantAContext = new AppDbContext(options, new TestTenantContext { TenantId = tenantA }))
        {
            var users = await tenantAContext.Users.AsNoTracking().ToListAsync();
            var brandings = await tenantAContext.TenantBrandings.AsNoTracking().ToListAsync();

            Assert.Single(users);
            Assert.All(users, x => Assert.Equal(tenantA, x.TenantId));
            Assert.Single(brandings);
            Assert.All(brandings, x => Assert.Equal(tenantA, x.TenantId));
        }

        await using (var emptyTenantContext = new AppDbContext(options, new TestTenantContext { TenantId = Guid.Empty }))
        {
            Assert.Empty(await emptyTenantContext.Users.AsNoTracking().ToListAsync());
            Assert.Empty(await emptyTenantContext.TenantBrandings.AsNoTracking().ToListAsync());
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }
        public string TenantSlug { get; set; } = string.Empty;
    }
}
