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
/// Delete and archive.
///
/// These tests exist because the failure they guard against is silent. Every foreign key
/// involved here defaulted to Cascade, so a delete endpoint written without a dependency
/// check would not have thrown — it would have returned 204 and taken the customer's
/// vehicles, jobs and bookings with them. A status-only assertion would have passed while
/// the data was being destroyed, so every test below asserts the STORED ROWS as well.
/// </summary>
public class CrudTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CrudTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record IdPayload(Guid Id);
    private sealed record LoginPayload(string Token);
    private sealed record CatalogueEntry(Guid Id, string Name);
    private sealed record CatalogueVariant(Guid Id, string Label);
    private sealed record PagedCustomers(List<IdPayload> Items, int Total);

    private async Task<HttpClient> CreateTenantAsync(string label)
    {
        var email = $"crud-{label}-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"CRUD Garage {label}",
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
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = (await login.Content.ReadFromJsonAsync<LoginPayload>())!.Token;
        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authed;
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, phone = "01234567890" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private static async Task<Guid> CreateVehicleAsync(HttpClient client, Guid customerId)
    {
        var makes = await client.GetFromJsonAsync<List<CatalogueEntry>>("/api/catalogue/makes");
        var models = await client.GetFromJsonAsync<List<CatalogueEntry>>(
            $"/api/catalogue/makes/{makes!.First().Id}/models");
        var years = await client.GetFromJsonAsync<List<int>>(
            $"/api/catalogue/models/{models!.First().Id}/years");
        var year = years!.First();
        var variants = await client.GetFromJsonAsync<List<CatalogueVariant>>(
            $"/api/catalogue/models/{models.First().Id}/variants?year={year}");

        var response = await client.PostAsJsonAsync("/api/vehicles", new
        {
            customerId,
            variantId = variants!.First().Id,
            year,
            registration = $"CR{Guid.NewGuid():N}"[..8]
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    [Fact]
    public async Task DeleteCustomer_WithNothingAttached_RemovesTheRow()
    {
        var client = await CreateTenantAsync("a");
        var customerId = await CreateCustomerAsync(client, "Disposable Customer");

        var response = await client.DeleteAsync($"/api/customers/{customerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Id == customerId);
        exists.Should().BeFalse("nothing referenced this customer, so a hard delete is safe");
    }

    /// <summary>
    /// The important one. Before the FKs were changed to Restrict this would have deleted
    /// the customer AND their vehicle, and reported success.
    /// </summary>
    [Fact]
    public async Task DeleteCustomer_WithAVehicle_IsRefusedAndDestroysNothing()
    {
        var client = await CreateTenantAsync("b");
        var customerId = await CreateCustomerAsync(client, "Customer With History");
        var vehicleId = await CreateVehicleAsync(client, customerId);

        var response = await client.DeleteAsync($"/api/customers/{customerId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Id == customerId))
            .Should().BeTrue("the customer must survive a refused delete");
        (await db.Vehicles.IgnoreQueryFilters().AnyAsync(v => v.Id == vehicleId))
            .Should().BeTrue("the vehicle must not have been cascaded away");
    }

    [Fact]
    public async Task ArchivedCustomer_LeavesTheListButStaysReadableById()
    {
        var client = await CreateTenantAsync("c");
        var customerId = await CreateCustomerAsync(client, "Archivable Customer");
        await CreateVehicleAsync(client, customerId);

        var archive = await client.PostAsync($"/api/customers/{customerId}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<PagedCustomers>("/api/customers");
        list!.Items.Should().NotContain(c => c.Id == customerId,
            "archived customers are hidden from the list");

        var detail = await client.GetAsync($"/api/customers/{customerId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK,
            "archiving hides a record; it must not make historical jobs unreadable");

        var withArchived = await client.GetFromJsonAsync<PagedCustomers>("/api/customers?includeArchived=true");
        withArchived!.Items.Should().Contain(c => c.Id == customerId);
    }

    [Fact]
    public async Task UnarchiveCustomer_ReturnsThemToTheList()
    {
        var client = await CreateTenantAsync("d");
        var customerId = await CreateCustomerAsync(client, "Returning Customer");

        await client.PostAsync($"/api/customers/{customerId}/archive", null);
        await client.PostAsync($"/api/customers/{customerId}/unarchive", null);

        var list = await client.GetFromJsonAsync<PagedCustomers>("/api/customers");
        list!.Items.Should().Contain(c => c.Id == customerId);
    }

    [Fact]
    public async Task DeleteJob_WhileDraft_RemovesTheRow()
    {
        var client = await CreateTenantAsync("e");
        var customerId = await CreateCustomerAsync(client, "Job Customer");
        var vehicleId = await CreateVehicleAsync(client, customerId);

        var created = await client.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Mistyped job",
            priority = "Normal"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var jobId = (await created.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var response = await client.DeleteAsync($"/api/jobs/{jobId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "a Draft job has no billing or stock history to lose");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Jobs.IgnoreQueryFilters().AnyAsync(j => j.Id == jobId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteJob_OnceScheduled_IsRefusedAndTheJobSurvives()
    {
        var client = await CreateTenantAsync("f");
        var customerId = await CreateCustomerAsync(client, "Scheduled Job Customer");
        var vehicleId = await CreateVehicleAsync(client, customerId);

        // Supplying a start date is what makes the job Scheduled rather than Draft.
        var created = await client.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Real work",
            priority = "Normal",
            scheduledStartUtc = DateTime.UtcNow.AddDays(1),
            scheduledEndUtc = DateTime.UtcNow.AddDays(1).AddHours(2)
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var jobId = (await created.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var response = await client.DeleteAsync($"/api/jobs/{jobId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Jobs.IgnoreQueryFilters().AnyAsync(j => j.Id == jobId))
            .Should().BeTrue("a refused delete must leave the job intact");
    }

    [Fact]
    public async Task DeleteCustomer_InAnotherTenant_ReturnsNotFoundAndLeavesItAlone()
    {
        var tenantA = await CreateTenantAsync("g");
        var tenantB = await CreateTenantAsync("h");

        var customerId = await CreateCustomerAsync(tenantA, "Someone Else's Customer");

        var response = await tenantB.DeleteAsync($"/api/customers/{customerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Id == customerId))
            .Should().BeTrue("a cross-tenant delete must not remove the row");
    }
}
