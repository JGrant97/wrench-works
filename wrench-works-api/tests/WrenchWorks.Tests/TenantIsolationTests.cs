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
    private sealed record CatalogueEntry(Guid Id, string Name);
    private sealed record CatalogueVariant(Guid Id, string Label);

    /// <summary>
    /// Walks the catalogue cascade to find any usable variant, exactly as the UI does.
    /// Vehicles are catalogue-backed now (see docs/vehicle-catalogue.md), so a test that
    /// needs a vehicle needs a variant first. Throws loudly if the seeder didn't run —
    /// an empty catalogue would otherwise surface as a confusing 400 further down.
    /// </summary>
    private static async Task<(Guid VariantId, int Year)> FirstCatalogueVariantAsync(HttpClient client)
    {
        var makes = await client.GetFromJsonAsync<List<CatalogueEntry>>("/api/catalogue/makes") ?? [];

        foreach (var make in makes)
        {
            var models = await client.GetFromJsonAsync<List<CatalogueEntry>>(
                $"/api/catalogue/makes/{make.Id}/models") ?? [];

            foreach (var model in models)
            {
                var years = await client.GetFromJsonAsync<List<int>>(
                    $"/api/catalogue/models/{model.Id}/years") ?? [];
                if (years.Count == 0) continue;

                var year = years[0];
                var variants = await client.GetFromJsonAsync<List<CatalogueVariant>>(
                    $"/api/catalogue/models/{model.Id}/variants?year={year}") ?? [];

                if (variants.Count > 0) return (variants[0].Id, year);
            }
        }

        throw new InvalidOperationException(
            "Vehicle catalogue is empty — VehicleCatalogueSeeder did not run.");
    }

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

        var (variantId, year) = await FirstCatalogueVariantAsync(client);

        var vehicleResponse = await client.PostAsJsonAsync("/api/vehicles", new
        {
            customerId,
            variantId,
            year,
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

    /// <summary>
    /// Creates a customer, a vehicle and a zone, and returns the ids a job needs.
    /// Separate from SeedJobWithLaborAsync because the zone tests need the pieces
    /// before a job exists, not after.
    /// </summary>
    private static async Task<(Guid CustomerId, Guid VehicleId, Guid ZoneId)> SeedJobPartsAsync(
        HttpClient client, string zoneName)
    {
        var customerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Zone Test Customer",
            phone = "01234567890"
        });
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var (variantId, year) = await FirstCatalogueVariantAsync(client);

        var vehicleResponse = await client.PostAsJsonAsync("/api/vehicles", new
        {
            customerId,
            variantId,
            year,
            registration = $"ZN{Guid.NewGuid():N}"[..8]
        });
        vehicleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicleId = (await vehicleResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var zoneResponse = await client.PostAsJsonAsync("/api/zones", new
        {
            name = zoneName,
            color = "#6b7280",
            capacity = 1
        });
        zoneResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var zoneId = (await zoneResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        return (customerId, vehicleId, zoneId);
    }

    /// <summary>
    /// The job endpoints validated CustomerId and VehicleId through the tenant filter but
    /// assigned AssignedZoneId with no lookup at all, so another business's zone was
    /// accepted outright — the database FK is satisfied and knows nothing about tenancy.
    /// The damage came later: a booking was auto-created on the foreign zone, and the
    /// calendar's unconditional b.Zone.Name projection then dereferenced a filtered-out
    /// zone and 500'd the whole list. See docs/review-findings.md finding 2.
    /// </summary>
    [Fact]
    public async Task CreateJob_WithAnotherTenantsZone_IsRejected()
    {
        var tenantA = await CreateTenantAsync("zone-a");
        var tenantB = await CreateTenantAsync("zone-b");

        var (_, _, foreignZoneId) = await SeedJobPartsAsync(tenantA, "Ramp A");
        var (customerId, vehicleId, _) = await SeedJobPartsAsync(tenantB, "Ramp B");

        var response = await tenantB.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Job on a stolen zone",
            priority = "Normal",
            zoneId = foreignZoneId
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a zone outside the caller's business does not exist as far as they are concerned");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var leaked = await db.Jobs.IgnoreQueryFilters()
            .AnyAsync(j => j.AssignedZoneId == foreignZoneId);
        leaked.Should().BeFalse("no job row may reference another tenant's zone");
    }

    [Fact]
    public async Task UpdateJob_WithAnotherTenantsZone_IsRejectedAndLeavesTheJobIntact()
    {
        var tenantA = await CreateTenantAsync("zone-c");
        var tenantB = await CreateTenantAsync("zone-d");

        var (_, _, foreignZoneId) = await SeedJobPartsAsync(tenantA, "Ramp C");
        var (customerId, vehicleId, ownZoneId) = await SeedJobPartsAsync(tenantB, "Ramp D");

        var created = await tenantB.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Legitimate job",
            priority = "Normal",
            zoneId = ownZoneId
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var jobId = (await created.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var response = await tenantB.PutAsJsonAsync($"/api/jobs/{jobId}", new
        {
            title = "Legitimate job",
            priority = "Normal",
            zoneId = foreignZoneId
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Status alone is not enough: assert the stored row still points at the caller's
        // own zone, since the update mutates the tracked entity before saving.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.Jobs.IgnoreQueryFilters().SingleAsync(j => j.Id == jobId);
        job.AssignedZoneId.Should().Be(ownZoneId, "the rejected update must not have been persisted");
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
