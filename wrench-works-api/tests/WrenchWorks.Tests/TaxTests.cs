using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Tests;

/// <summary>
/// Tax arithmetic.
///
/// Two things here are worth more than the rest: that inclusive pricing divides the tax out
/// rather than adding it on, and that rounding is half away from zero. Both fail silently —
/// the number still looks like money.
/// </summary>
public class TaxCalculatorTests
{
    [Fact]
    public void Exclusive_AddsTaxOnTop()
    {
        var result = TaxCalculator.CalculateLine(new TaxableLine(100m, 0.20m), pricesIncludeTax: false);

        result.Net.Should().Be(100m);
        result.Tax.Should().Be(20m);
        result.Gross.Should().Be(120m);
    }

    /// <summary>
    /// The branch that is easy to get wrong: £120 quoted "including VAT" is £100 + £20, not
    /// £120 + £24. Implementing only the exclusive formula overcharges by the tax on the tax.
    /// </summary>
    [Fact]
    public void Inclusive_DividesTaxOut()
    {
        var result = TaxCalculator.CalculateLine(new TaxableLine(120m, 0.20m), pricesIncludeTax: true);

        result.Net.Should().Be(100m);
        result.Tax.Should().Be(20m);
        result.Gross.Should().Be(120m);
    }

    /// <summary>
    /// Tax is taken as the remainder on the inclusive path precisely so this holds. If it
    /// were rounded independently the parts would not add up to what the customer pays.
    /// </summary>
    [Theory]
    [InlineData(99.99, 0.20)]
    [InlineData(12.34, 0.0875)]
    [InlineData(0.01, 0.20)]
    [InlineData(1234.56, 0.19)]
    public void Inclusive_NetPlusTaxAlwaysEqualsGross(decimal gross, decimal rate)
    {
        var result = TaxCalculator.CalculateLine(new TaxableLine(gross, rate), pricesIncludeTax: true);

        (result.Net + result.Tax).Should().Be(result.Gross);
    }

    /// <summary>
    /// Half away from zero, not .NET's default banker's rounding — which would turn
    /// 0.125 into 0.12 and make an invoice a penny light.
    /// </summary>
    [Fact]
    public void Rounding_IsHalfAwayFromZero()
    {
        // 2.50 * 0.05 = 0.125 exactly.
        var result = TaxCalculator.CalculateLine(new TaxableLine(2.50m, 0.05m), pricesIncludeTax: false);

        result.Tax.Should().Be(0.13m, "banker's rounding would give 0.12");
    }

    [Fact]
    public void ZeroRate_ProducesNoTax()
    {
        var result = TaxCalculator.CalculateLine(new TaxableLine(250m, 0m), pricesIncludeTax: false);

        result.Tax.Should().Be(0m);
        result.Gross.Should().Be(250m);
    }

    /// <summary>
    /// A real US rate. Storing the rate at 2dp would make this 8.88% and quietly change
    /// every total.
    /// </summary>
    [Fact]
    public void FourDecimalRate_IsAppliedExactly()
    {
        var result = TaxCalculator.CalculateLine(new TaxableLine(1000m, 0.08875m), pricesIncludeTax: false);

        result.Tax.Should().Be(88.75m);
    }

    [Fact]
    public void Total_RoundsPerLineThenSums()
    {
        // Three lines each rounding up a half-penny. Taxing the sum instead would give 0.38.
        var lines = new[]
        {
            new TaxableLine(2.50m, 0.05m),
            new TaxableLine(2.50m, 0.05m),
            new TaxableLine(2.50m, 0.05m),
        };

        TaxCalculator.Total(lines, pricesIncludeTax: false).Tax.Should().Be(0.39m);
    }
}

/// <summary>End-to-end: rates, snapshotting, and exemption.</summary>
public class TaxTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TaxTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record IdPayload(Guid Id);
    private sealed record LoginPayload(string Token);
    private sealed record CatalogueEntry(Guid Id, string Name);
    private sealed record CatalogueVariant(Guid Id, string Label);
    private sealed record TaxRatePayload(Guid Id, string Name, decimal Rate, List<string> Categories);
    private sealed record LaborLine(Guid Id, string Description, decimal Hours, decimal Rate, decimal Total, decimal TaxRatePercent, decimal TaxAmount);
    private sealed record TaxComponentLine(string Name, decimal RatePercent);
    private sealed record TaxLine(string Name, decimal RatePercent, decimal Amount, List<TaxComponentLine> Components);
    private sealed record JobDetail(Guid Id, decimal LaborTotal, decimal PartsTotal, decimal GrandTotal, decimal SubTotal, decimal TaxTotal, string TaxLabel, bool PricesIncludeTax, bool CustomerIsTaxExempt, List<TaxLine> TaxBreakdown);

    private async Task<HttpClient> CreateTenantAsync(string label)
    {
        var email = $"tax-{label}-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Tax Garage {label}",
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

    private static async Task<Guid> CreateRateAsync(
        HttpClient client, string name, decimal rate,
        string[]? categories = null, object[]? components = null)
    {
        var response = await client.PostAsJsonAsync("/api/tax/rates", new
        {
            name,
            rate,
            categories = categories ?? ["Labour", "Parts"],
            components
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private async Task<Guid> CreateJobAsync(HttpClient client, bool taxExemptCustomer = false)
    {
        var customer = await client.PostAsJsonAsync("/api/customers", new
        {
            name = $"Tax Customer {Guid.NewGuid():N}"[..20],
            phone = $"07{Random.Shared.NextInt64(100000000, 999999999)}"
        });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customer.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        if (taxExemptCustomer)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customerId);
            entity.IsTaxExempt = true;
            entity.TaxExemptionReference = "CERT-123";
            await db.SaveChangesAsync();
        }

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
            registration = $"TX{Guid.NewGuid():N}"[..8]
        });
        vehicle.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicleId = (await vehicle.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var job = await client.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Taxable work",
            priority = "Normal"
        });
        job.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await job.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    // The exemption flag has no write endpoint yet — the customer update DTO does not
    // carry it — so this sets it directly. Noted rather than hidden: if an endpoint gains
    // the field, this must go through it instead. See the lesson in CLAUDE.md about tests
    // that fake a precondition and thereby prove nothing.

    [Fact]
    public async Task Rate_AboveOne_IsRejected()
    {
        var client = await CreateTenantAsync("range");

        var response = await client.PostAsJsonAsync("/api/tax/rates", new
        {
            name = "Typed twenty instead of nought point two",
            rate = 20m,
            categories = new[] { "Labour", "Parts" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "20 means 2000%, which is the commonest data-entry error here");
    }

    [Fact]
    public async Task LabourLine_TakesTheConfiguredRate()
    {
        var client = await CreateTenantAsync("labour");
        await CreateRateAsync(client, "VAT Standard", 0.20m);
        var jobId = await CreateJobAsync(client);

        var line = await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Diagnostics",
            hours = 2m,
            rate = 50m
        });
        line.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await line.Content.ReadFromJsonAsync<LaborLine>();
        payload!.TaxRatePercent.Should().Be(0.20m);
        payload.TaxAmount.Should().Be(20m, "2h x 50 = 100, plus 20%");
    }

    /// <summary>
    /// A US shop configures a parts rate and no labour rate. That is not a missing setting —
    /// it is the shop saying labour is not taxable there.
    /// </summary>
    [Fact]
    public async Task LabourIsUntaxed_WhenOnlyPartsHasARate()
    {
        var client = await CreateTenantAsync("partsonly");
        await CreateRateAsync(client, "NY Sales Tax", 0.08875m, categories: ["Parts"]);
        var jobId = await CreateJobAsync(client);

        var line = await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Labour",
            hours = 1m,
            rate = 100m
        });

        var payload = await line.Content.ReadFromJsonAsync<LaborLine>();
        payload!.TaxRatePercent.Should().Be(0m);
        payload.TaxAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ExemptCustomer_IsChargedNoTax()
    {
        var client = await CreateTenantAsync("exempt");
        await CreateRateAsync(client, "VAT Standard", 0.20m);
        var jobId = await CreateJobAsync(client, taxExemptCustomer: true);

        var line = await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Work for an exempt customer",
            hours = 1m,
            rate = 100m
        });

        var payload = await line.Content.ReadFromJsonAsync<LaborLine>();
        payload!.TaxAmount.Should().Be(0m);

        var job = await client.GetFromJsonAsync<JobDetail>($"/api/jobs/{jobId}");
        job!.CustomerIsTaxExempt.Should().BeTrue();
        job.TaxTotal.Should().Be(0m);
    }

    /// <summary>
    /// The whole reason the rate is snapshotted on the line. When UK VAT moved, systems
    /// that recomputed from current settings silently rewrote their own invoice history.
    /// </summary>
    [Fact]
    public async Task ChangingTheRate_DoesNotAlterAJobAlreadyRaised()
    {
        var client = await CreateTenantAsync("snapshot");
        var rateId = await CreateRateAsync(client, "VAT Standard", 0.20m);
        var jobId = await CreateJobAsync(client);

        await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Work at 20%",
            hours = 1m,
            rate = 100m
        });

        var before = await client.GetFromJsonAsync<JobDetail>($"/api/jobs/{jobId}");
        before!.TaxTotal.Should().Be(20m);

        var update = await client.PutAsJsonAsync($"/api/tax/rates/{rateId}", new
        {
            name = "VAT Standard",
            rate = 0.25m,
            categories = new[] { "Labour", "Parts" }
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await client.GetFromJsonAsync<JobDetail>($"/api/jobs/{jobId}");
        after!.TaxTotal.Should().Be(20m, "the line snapshotted 20% and must keep it");
        after.GrandTotal.Should().Be(120m);
    }

    [Fact]
    public async Task JobDetail_BreaksTaxDownByRate_WithJurisdictionComponents()
    {
        var client = await CreateTenantAsync("breakdown");
        await CreateRateAsync(client, "NY State + NYC", 0.08875m, components:
        [
            new { name = "NY State", rate = 0.04m, sortOrder = 0 },
            new { name = "NYC", rate = 0.045m, sortOrder = 1 },
            new { name = "MCTD", rate = 0.00375m, sortOrder = 2 },
        ]);

        var jobId = await CreateJobAsync(client);
        await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Work",
            hours = 1m,
            rate = 1000m
        });

        var job = await client.GetFromJsonAsync<JobDetail>($"/api/jobs/{jobId}");

        job!.TaxBreakdown.Should().HaveCount(1);
        var line = job.TaxBreakdown[0];
        line.Name.Should().Be("NY State + NYC");
        line.Amount.Should().Be(88.75m);
        line.Components.Should().HaveCount(3);
        line.Components[0].Name.Should().Be("NY State", "components are ordered widest first");
        line.Components.Sum(c => c.RatePercent).Should().Be(0.08875m);
    }

    [Fact]
    public async Task JobWithNoTax_ReturnsAnEmptyBreakdown()
    {
        var client = await CreateTenantAsync("notax");
        var jobId = await CreateJobAsync(client);

        await client.PostAsJsonAsync($"/api/jobs/{jobId}/labor", new
        {
            description = "Untaxed work",
            hours = 1m,
            rate = 100m
        });

        var job = await client.GetFromJsonAsync<JobDetail>($"/api/jobs/{jobId}");

        job!.TaxTotal.Should().Be(0m);
        job.TaxBreakdown.Should().BeEmpty();
        job.GrandTotal.Should().Be(100m, "a business with no rates configured charges no tax");
    }

    [Fact]
    public async Task Rates_AreNotVisibleToAnotherTenant()
    {
        var tenantA = await CreateTenantAsync("iso-a");
        var tenantB = await CreateTenantAsync("iso-b");

        await CreateRateAsync(tenantA, "Tenant A VAT", 0.20m);

        var rates = await tenantB.GetFromJsonAsync<List<TaxRatePayload>>("/api/tax/rates");

        rates.Should().BeEmpty("tax rates are tenant-scoped");
    }

    [Fact]
    public async Task SettingADefault_ClearsThePrevious()
    {
        var client = await CreateTenantAsync("defaults");
        await CreateRateAsync(client, "Old VAT", 0.175m);
        await CreateRateAsync(client, "New VAT", 0.20m);

        var rates = await client.GetFromJsonAsync<List<TaxRatePayload>>("/api/tax/rates");

        rates!.Count(r => r.Categories.Contains("Parts")).Should().Be(1,
            "the unique index on (BusinessId, Category) makes two impossible");
        rates.Single(r => r.Categories.Contains("Parts")).Name.Should().Be("New VAT");
    }

    [Fact]
    public async Task UnknownCategory_IsRejected()
    {
        var client = await CreateTenantAsync("badcat");

        var response = await client.PostAsJsonAsync("/api/tax/rates", new
        {
            name = "Mistyped",
            rate = 0.2m,
            categories = new[] { "Labor" }   // American spelling; the enum is "Labour"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a category that matches nothing would silently apply to no lines at all");
    }

    /// <summary>
    /// The reason consumables exist as their own category: a shop can tax parts and
    /// consumables at different rates, or tax one and not the other. Before this, both
    /// billed through JobPartLine and necessarily took the same rate.
    /// </summary>
    [Fact]
    public async Task ConsumablesTakeTheirOwnRate_NotThePartsRate()
    {
        var client = await CreateTenantAsync("consumable");
        await CreateRateAsync(client, "Parts Tax", 0.20m, categories: ["Parts"]);
        await CreateRateAsync(client, "Supplies Levy", 0.05m, categories: ["Consumables"]);

        var jobId = await CreateJobAsync(client);

        var partId = await CreateInventoryItemAsync(client, "Brake Pads", 100m, isConsumable: false);
        var supplyId = await CreateInventoryItemAsync(client, "Shop Rags", 100m, isConsumable: true);

        var part = await AddPartAsync(client, jobId, partId);
        var supply = await AddPartAsync(client, jobId, supplyId);

        part!.TaxRatePercent.Should().Be(0.20m, "a fitted part takes the Parts rate");
        supply!.TaxRatePercent.Should().Be(0.05m, "a consumable takes the Consumables rate");
    }

    /// <summary>
    /// Several US states tax parts while treating shop supplies as consumed by the shop,
    /// so nothing is charged onward. No Consumables mapping is how that is expressed.
    /// </summary>
    [Fact]
    public async Task ConsumablesAreUntaxed_WhenNoRateIsMappedToThem()
    {
        var client = await CreateTenantAsync("consumable-exempt");
        await CreateRateAsync(client, "Parts Tax", 0.20m, categories: ["Parts"]);

        var jobId = await CreateJobAsync(client);
        var supplyId = await CreateInventoryItemAsync(client, "Degreaser", 50m, isConsumable: true);

        var supply = await AddPartAsync(client, jobId, supplyId);

        supply!.TaxRatePercent.Should().Be(0m);
        supply.TaxAmount.Should().Be(0m);
    }

    private static async Task<Guid> CreateInventoryItemAsync(
        HttpClient client, string name, decimal price, bool isConsumable)
    {
        var response = await client.PostAsJsonAsync("/api/inventory/items", new
        {
            name = $"{name} {Guid.NewGuid():N}"[..24],
            sku = (string?)null,
            categoryId = (Guid?)null,
            unitCost = price / 2,
            retailPrice = price,
            stockOnHand = 100,
            reorderThreshold = 5,
            compatibilityTagsJson = (string?)null,
            isConsumable
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private sealed record PartLinePayload(Guid Id, decimal Quantity, decimal UnitPrice, decimal Total, decimal TaxRatePercent, decimal TaxAmount);

    private static async Task<PartLinePayload?> AddPartAsync(HttpClient client, Guid jobId, Guid itemId)
    {
        var response = await client.PostAsJsonAsync($"/api/jobs/{jobId}/parts", new
        {
            inventoryItemId = itemId,
            quantity = 1m,
            unitPriceOverride = (decimal?)null
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, "add part returned: {0}", body);

        return await response.Content.ReadFromJsonAsync<PartLinePayload>();
    }
}
