using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitaent.Infrastructure.Persistence;

namespace Vitaent.Api.Tests;

public class AuthFlowIntegrationTests : IClassFixture<AuthFlowFactory>
{
    private readonly AuthFlowFactory _factory;

    public AuthFlowIntegrationTests(AuthFlowFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SignIn_ThenRefresh_RotatesRefreshCookie()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        using var client = _factory.CreateDefaultClient(handler);

        var signInResponse = await client.PostAsJsonAsync("/api/auth/sign-in?tenant=clinic1", new
        {
            email = "admin@clinic1.local",
            password = "Admin123!"
        });

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);

        var signInPayload = await signInResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(signInPayload.TryGetProperty("accessToken", out var signInToken));
        Assert.False(string.IsNullOrWhiteSpace(signInToken.GetString()));

        var firstSetCookie = signInResponse.Headers.TryGetValues("Set-Cookie", out var signInCookies)
            ? signInCookies.FirstOrDefault(x => x.Contains("vitaent_refresh="))
            : null;

        Assert.False(string.IsNullOrWhiteSpace(firstSetCookie));

        var refreshResponse = await client.PostAsync("/api/auth/refresh?tenant=clinic1", new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(refreshPayload.TryGetProperty("accessToken", out var refreshToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken.GetString()));

        var secondSetCookie = refreshResponse.Headers.TryGetValues("Set-Cookie", out var refreshCookies)
            ? refreshCookies.FirstOrDefault(x => x.Contains("vitaent_refresh="))
            : null;

        Assert.False(string.IsNullOrWhiteSpace(secondSetCookie));
        Assert.NotEqual(firstSetCookie, secondSetCookie);
    }
}

public class AuthFlowFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase($"auth-flow-{Guid.NewGuid()}");
            });
        });
    }
}
