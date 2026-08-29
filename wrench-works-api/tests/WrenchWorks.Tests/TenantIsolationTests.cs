using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Tests;

/// <summary>
/// Cross-tenant isolation tests.
///
/// JobLaborLine, JobPartLine, JobAssignment, BusinessUserRole and RolePermission
/// have DbSets but no global query filter (EF reports this as warning 10622 on
/// every startup). The job line-item endpoints load lines from those unfiltered
/// sets and rely on a separate db.Jobs.FindAsync(id) call for the tenant check.
/// These tests verify that indirection actually holds the tenant boundary.
/// </summary>
public class TenantIsolationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record IdPayload(Guid Id);
    private sealed record LoginPayload(string Token);

    /// <summary>Registers a business, verifies its owner, and returns an authenticated client.</summary>
    private async Task<HttpClient> CreateTenantAsync(string label)
    {
        var email = $"{label}-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Garage {label}",
            ownerName = $"Owner {label}",
            email,
            password = Password
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        // Registration sends a verification email and login is blocked until it's used.
        // Flip the flag directly rather than scraping ConsoleEmailSender output.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.EmailVerified = true;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = (await login.Content.ReadFromJsonAsync<LoginPayload>())!.Token;

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authed;
    }

    /// <summary>Creates customer + vehicle + job + one labor line for the given tenant.</summary>
    private static async Task<(Guid JobId, Guid LaborLineId)> SeedJobWithLaborAsync(HttpClient client)
    {
        var customerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Test Customer",
            phone = "01234567890"
        });
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var vehicleResponse = await client.PostAsJsonAsync("/api/vehicles", new
        {
            customerId,
            make = "Ford",
            model = "Focus",
            registration = "AB12CDE"
        });
        vehicleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicleId = (await vehicleResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var jobResponse = await client.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Front brake pads",
            priority = "Normal"
        });
        jobResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var jobId = (await jobResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var laborResponse = await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Diagnostics",
            hours = 1.5m,
            rate = 60m
        });
        laborResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var laborLineId = (await laborResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        return (jobId, laborLineId);
    }

    [Fact]
    public async Task GetJob_BelongingToAnotherTenant_ReturnsNotFound()
    {
        var tenantA = await CreateTenantAsync("a");
        var tenantB = await CreateTenantAsync("b");

        var (jobId, _) = await SeedJobWithLaborAsync(tenantA);

        var response = await tenantB.GetAsync($"/api/jobs/{jobId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the global query filter on Job should hide another business's job");
    }

    [Fact]
    public async Task RemoveLaborLine_OnAnotherTenantsJob_IsRejectedAndLeavesTheLineIntact()
    {
        var tenantA = await CreateTenantAsync("a");
        var tenantB = await CreateTenantAsync("b");

        var (jobId, laborLineId) = await SeedJobWithLaborAsync(tenantA);

        var response = await tenantB.DeleteAsync($"/api/jobs/{jobId}/labor/{laborLineId}");

        response.StatusCode.Should().NotBe(HttpStatusCode.NoContent,
            "tenant B must not be able to delete a labor line on tenant A's job");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The status code alone isn't enough — confirm the row survived.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stillThere = await db.JobLaborLines.IgnoreQueryFilters()
            .AnyAsync(l => l.Id == laborLineId);

        stillThere.Should().BeTrue("the labor line must survive a cross-tenant delete attempt");
    }

    [Fact]
    public async Task AddLaborLine_ToAnotherTenantsJob_IsRejected()
    {
        var tenantA = await CreateTenantAsync("a");
        var tenantB = await CreateTenantAsync("b");

        var (jobId, _) = await SeedJobWithLaborAsync(tenantA);

        var response = await tenantB.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Injected by another tenant",
            hours = 1m,
            rate = 100m
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.Created,
            "tenant B must not be able to append labor to tenant A's job");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lineCount = await db.JobLaborLines.IgnoreQueryFilters()
            .CountAsync(l => l.JobId == jobId);

        lineCount.Should().Be(1, "only tenant A's original labor line should exist");
    }
}
