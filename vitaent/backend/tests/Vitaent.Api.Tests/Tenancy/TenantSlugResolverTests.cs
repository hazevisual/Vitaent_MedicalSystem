using Vitaent.Api.Tenancy;

namespace Vitaent.Api.Tests.Tenancy;

public class TenantSlugResolverTests
{
    private readonly TenantSlugResolver _resolver = new();

    [Fact]
    public void ResolveSlug_SubdomainHost_ReturnsSlug()
    {
        var result = _resolver.ResolveSlug("clinic1.localhost", null);

        Assert.Equal("clinic1", result);
    }

    [Fact]
    public void ResolveSlug_SubdomainHostWithPort_ReturnsSlug()
    {
        var result = _resolver.ResolveSlug("clinic1.localhost:5173", null);

        Assert.Equal("clinic1", result);
    }

    [Fact]
    public void ResolveSlug_LocalhostOnly_ReturnsNull()
    {
        var result = _resolver.ResolveSlug("localhost", null);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveSlug_QueryTenant_OverridesHost()
    {
        var result = _resolver.ResolveSlug("wrong.localhost", "clinic1");

        Assert.Equal("clinic1", result);
    }

    [Fact]
    public void ResolveSlug_MultiLevelHost_ReturnsFirstLabel()
    {
        var result = _resolver.ResolveSlug("clinic1.dev.localhost", null);

        Assert.Equal("clinic1", result);
    }
}
