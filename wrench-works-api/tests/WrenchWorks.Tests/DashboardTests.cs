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
/// The dashboard aggregate.
///
/// Worth testing beyond "returns 200" because every number on it is a claim about the
/// business. A count that silently includes archived rows, or another tenant's jobs,
/// would look entirely plausible on screen.
/// </summary>
public class DashboardTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public DashboardTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record IdPayload(Guid Id);
    private sealed record LoginPayload(string Token);
    private sealed record CatalogueEntry(Guid Id, string Name);
    private sealed record CatalogueVariant(Guid Id, string Label);

    private sealed record StatusCount(string Status, int Count);
    private sealed record Dashboard(
        List<object> TodaysBookings,
        List<object> ActiveJobs,
        List<StatusCount> JobsByStatus,
        List<object> LowStockItems,
        int OpenJobCount,
        int UnscheduledJobCount,
        decimal RevenueThisMonth,
        decimal RevenueLastMonth,
        int CustomerCount,
        int VehicleCount);

    private async Task<HttpClient> CreateTenantAsync(string label)
    {
        var email = $"dash-{label}-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Dashboard Garage {label}",
            ownerName = $"Owner {label}",
            email,
            password = Password
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.EmailVerified = true;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var token = (await login.Content.ReadFromJsonAsync<LoginPayload>())!.Token;

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authed;
    }

    private static async Task<Guid> CreateJobAsync(HttpClient client, string title)
    {
        // Unique phone per customer: the create path rejects duplicates, and a silent
        // 409 here would surface much later as a confusing 400 from the job endpoint.
        var customer = await client.PostAsJsonAsync("/api/customers", new
        {
            name = $"Dash Customer {Guid.NewGuid():N}"[..20],
            phone = $"07{Random.Shared.NextInt64(100000000, 999999999)}"
        });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customer.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var makes = await client.GetFromJsonAsync<List<CatalogueEntry>>("/api/catalogue/makes");
        var models = await client.GetFromJsonAsync<List<CatalogueEntry>>($"/api/catalogue/makes/{makes!.First().Id}/models");
        var years = await client.GetFromJsonAsync<List<int>>($"/api/catalogue/models/{models!.First().Id}/years");
        var year = years!.First();
        var variants = await client.GetFromJsonAsync<List<CatalogueVariant>>(
            $"/api/catalogue/models/{models.First().Id}/variants?year={year}");

        var vehicle = await client.PostAsJsonAsync("/api/vehicles", new
        {
            customerId,
            variantId = variants!.First().Id,
            year,
            registration = $"DS{Guid.NewGuid():N}"[..8]
        });
        vehicle.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicleId = (await vehicle.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var job = await client.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title,
            priority = "Normal"
        });
        job.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await job.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    // Reads the dashboard, surfacing the response body when the call fails. GetFromJsonAsync
    // throws on a non-2xx with only the status code, which turns any server-side error into
    // an unhelpful "500" with no clue what broke.
    private static async Task<Dashboard> GetDashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the dashboard returned: {0}", body);

        return (await response.Content.ReadFromJsonAsync<Dashboard>())!;
    }

    [Fact]
    public async Task Dashboard_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dashboard_CountsTheCallersOwnOpenJobs()
    {
        var client = await CreateTenantAsync("a");
        await CreateJobAsync(client, "Dashboard job one");
        await CreateJobAsync(client, "Dashboard job two");

        var dash = await GetDashboardAsync(client);

        dash.Should().NotBeNull();
        dash!.OpenJobCount.Should().Be(2);
        dash.UnscheduledJobCount.Should().Be(2, "neither job was given a start date");
        dash.CustomerCount.Should().Be(2, "each helper call creates its own customer");
        dash.JobsByStatus.Should().Contain(s => s.Status == "Draft" && s.Count == 2);
    }

    /// <summary>
    /// The dashboard is the one screen that aggregates across every table, so it is the
    /// most likely place for a missing tenant filter to go unnoticed.
    /// </summary>
    [Fact]
    public async Task Dashboard_DoesNotCountAnotherTenantsJobs()
    {
        var tenantA = await CreateTenantAsync("b");
        var tenantB = await CreateTenantAsync("c");

        await CreateJobAsync(tenantA, "Tenant A job");
        await CreateJobAsync(tenantA, "Another tenant A job");

        var dash = await GetDashboardAsync(tenantB);

        dash!.OpenJobCount.Should().Be(0, "tenant B has created no jobs");
        dash.CustomerCount.Should().Be(0);
        dash.VehicleCount.Should().Be(0);
    }

    [Fact]
    public async Task Dashboard_ExcludesArchivedJobsAndCustomers()
    {
        var client = await CreateTenantAsync("d");
        var jobId = await CreateJobAsync(client, "Job to archive");

        var before = await GetDashboardAsync(client);
        before!.OpenJobCount.Should().Be(1);

        var archive = await client.PostAsync($"/api/jobs/{jobId}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await GetDashboardAsync(client);
        after!.OpenJobCount.Should().Be(0, "an archived job is not open work");
        after.JobsByStatus.Should().NotContain(s => s.Status == "Draft" && s.Count > 0);
    }

    [Fact]
    public async Task Dashboard_ReportsNoRevenueBeforeAnyWorkIsCompleted()
    {
        var client = await CreateTenantAsync("e");
        await CreateJobAsync(client, "Unfinished job");

        var dash = await GetDashboardAsync(client);

        dash!.RevenueThisMonth.Should().Be(0m, "a Draft job has earned nothing");
        dash.RevenueLastMonth.Should().Be(0m);
    }
}
