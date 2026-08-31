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
/// Vehicle catalogue cascade.
///
/// The catalogue's whole promise is structural: an invalid combination is unreachable
/// rather than rejected, because no variant row exists for it. That promise is exactly
/// what an integration test can hold — assert that an MX-5 never offers Diesel and the
/// design either holds or it doesn't.
///
/// The variant-detail endpoint is tested here too because it is what lets an edit form
/// rebuild a selection it only knows by id. Before it existed the picker opened blank and
/// cleared the vehicle's variant on mount; see docs/review-findings.md finding 1.
/// </summary>
public class CatalogueTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogueTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record LoginPayload(string Token);
    private sealed record MakeDto(Guid Id, string Name);
    private sealed record ModelDto(Guid Id, string Name);
    private sealed record VariantDto(
        Guid Id, string Label, int YearFrom, int YearTo,
        string? Trim, string? BodyStyle,
        decimal? EngineDisplacementL, int? EngineCylinders,
        string FuelType, string Transmission, string? DriveType, string Market);
    private sealed record VariantDetailDto(
        Guid Id, Guid ModelId, string ModelName, Guid MakeId, string MakeName,
        string Label, int YearFrom, int YearTo,
        string? Trim, string? BodyStyle,
        decimal? EngineDisplacementL, int? EngineCylinders,
        string FuelType, string Transmission, string? DriveType, string Market);

    /// <summary>
    /// The catalogue is global reference data, but every endpoint requires vehicles.view,
    /// so a test still needs a real business and a real token.
    /// </summary>
    private async Task<HttpClient> CreateAuthedClientAsync(string label)
    {
        var email = $"cat-{label}-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Catalogue Garage {label}",
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

    /// <summary>Walks make → model for a seeded model, returning both ids.</summary>
    private static async Task<(Guid MakeId, Guid ModelId)> ResolveModelAsync(
        HttpClient client, string makeName, string modelName)
    {
        var makes = await client.GetFromJsonAsync<List<MakeDto>>("/api/catalogue/makes");
        var make = makes!.SingleOrDefault(m => m.Name == makeName);
        make.Should().NotBeNull($"the seeder is expected to have created the make {makeName}");

        var models = await client.GetFromJsonAsync<List<ModelDto>>(
            $"/api/catalogue/makes/{make!.Id}/models");
        var model = models!.SingleOrDefault(m => m.Name == modelName);
        model.Should().NotBeNull($"the seeder is expected to have created {makeName} {modelName}");

        return (make.Id, model!.Id);
    }

    [Fact]
    public async Task Catalogue_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/catalogue/makes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the deny-by-default FallbackPolicy applies — the catalogue is not AllowAnonymous");
    }

    /// <summary>
    /// The core claim of the design. Fuel is not validated by a rule anywhere; it is
    /// impossible because no MX-5 variant row carries Diesel. If this fails, either the
    /// seed grew a wrong row or the variants query stopped filtering by model.
    /// </summary>
    [Fact]
    public async Task Mx5_HasNoDieselVariant()
    {
        var client = await CreateAuthedClientAsync("mx5");
        var (_, modelId) = await ResolveModelAsync(client, "Mazda", "MX-5");

        var variants = await client.GetFromJsonAsync<List<VariantDto>>(
            $"/api/catalogue/models/{modelId}/variants");

        variants.Should().NotBeEmpty();
        variants!.Should().OnlyContain(v => v.FuelType != "Diesel",
            "no MX-5 was ever built as a diesel, so no such row may exist");
    }

    /// <summary>
    /// Per-model filtering, not a global ban on diesel — the Focus proves the catalogue
    /// distinguishes models rather than simply never returning Diesel.
    /// </summary>
    [Fact]
    public async Task Focus_DoesOfferDiesel()
    {
        var client = await CreateAuthedClientAsync("focus");
        var (_, modelId) = await ResolveModelAsync(client, "Ford", "Focus");

        var variants = await client.GetFromJsonAsync<List<VariantDto>>(
            $"/api/catalogue/models/{modelId}/variants");

        variants!.Should().Contain(v => v.FuelType == "Diesel",
            "the seeded Focus includes TDCi rows, so the MX-5 result is filtering, not a blanket exclusion");
    }

    [Fact]
    public async Task Years_AreLimitedToCataloguedRanges()
    {
        var client = await CreateAuthedClientAsync("years");
        var (_, modelId) = await ResolveModelAsync(client, "Mazda", "MX-5");

        var years = await client.GetFromJsonAsync<List<int>>(
            $"/api/catalogue/models/{modelId}/years");

        years.Should().NotBeEmpty();
        years!.Should().NotContain(1960, "the MX-5 did not exist in 1960");
        years!.Should().OnlyContain(y => y >= 1989 && y <= DateTime.UtcNow.Year + 1,
            "years come from real variant ranges, not an open-ended input");
    }

    [Fact]
    public async Task Variants_ForAYear_AllCoverThatYear()
    {
        var client = await CreateAuthedClientAsync("range");
        var (_, modelId) = await ResolveModelAsync(client, "Mazda", "MX-5");

        var years = await client.GetFromJsonAsync<List<int>>($"/api/catalogue/models/{modelId}/years");
        var year = years!.First();

        var variants = await client.GetFromJsonAsync<List<VariantDto>>(
            $"/api/catalogue/models/{modelId}/variants?year={year}");

        variants.Should().NotBeEmpty();
        variants!.Should().OnlyContain(v => v.YearFrom <= year && v.YearTo >= year);
    }

    /// <summary>
    /// The endpoint the edit form depends on: given only a variant id it must return
    /// enough to rebuild the whole cascade, or the picker cannot show what a vehicle is.
    /// </summary>
    [Fact]
    public async Task GetVariant_ReturnsItsModelAndMake()
    {
        var client = await CreateAuthedClientAsync("detail");
        var (makeId, modelId) = await ResolveModelAsync(client, "Mazda", "MX-5");

        var variants = await client.GetFromJsonAsync<List<VariantDto>>(
            $"/api/catalogue/models/{modelId}/variants");
        var expected = variants!.First();

        var detail = await client.GetFromJsonAsync<VariantDetailDto>(
            $"/api/catalogue/variants/{expected.Id}");

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(expected.Id);
        detail.ModelId.Should().Be(modelId, "the picker sets its Model dropdown from this");
        detail.MakeId.Should().Be(makeId, "the picker sets its Make dropdown from this");
        detail.MakeName.Should().Be("Mazda");
        detail.ModelName.Should().Be("MX-5");
        detail.FuelType.Should().Be(expected.FuelType);
        detail.Transmission.Should().Be(expected.Transmission);
        detail.EngineDisplacementL.Should().Be(expected.EngineDisplacementL,
            "the picker matches its Engine facet on this value, so it must round-trip exactly");
    }

    [Fact]
    public async Task GetVariant_UnknownId_Returns404()
    {
        var client = await CreateAuthedClientAsync("missing");

        var response = await client.GetAsync($"/api/catalogue/variants/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a missing variant is a NotFoundException, not an unhandled null dereference");
    }
}
