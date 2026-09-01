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
/// Business settings, and specifically the currency vocabulary.
///
/// The UI offers a dropdown of three, but a request need not come from the dropdown, and
/// an unrecognised code would reach every screen that formats money — where it renders as
/// the raw string beside the number with nothing on the server to notice.
/// </summary>
public class BusinessTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public BusinessTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record LoginPayload(string Token, LoginUser User);
    private sealed record LoginUser(Guid Id, string Name, string Email, Guid BusinessId, string BusinessName, string Currency);
    private sealed record BusinessPayload(Guid Id, string Name, string Timezone, string Currency);

    private async Task<HttpClient> CreateTenantAsync(string label)
    {
        var email = $"biz-{label}-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Currency Garage {label}",
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

    private static object UpdateBody(string currency) => new
    {
        name = "Currency Garage",
        address = (string?)null,
        phone = (string?)null,
        timezone = "Europe/London",
        currency,
        workingHoursJson = (string?)null
    };

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public async Task UpdateBusiness_AcceptsEverySupportedCurrency(string currency)
    {
        var client = await CreateTenantAsync($"ok-{currency}");

        var response = await client.PutAsJsonAsync("/api/business", UpdateBody(currency));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var business = await client.GetFromJsonAsync<BusinessPayload>("/api/business");
        business!.Currency.Should().Be(currency);
    }

    [Theory]
    [InlineData("JPY")]      // real currency, not one this product supports
    [InlineData("POUNDS")]   // a label rather than a code
    [InlineData("")]         // empty
    public async Task UpdateBusiness_RejectsAnythingElse(string currency)
    {
        var client = await CreateTenantAsync($"bad-{currency.Length}");

        var response = await client.PutAsJsonAsync("/api/business", UpdateBody(currency));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an unsupported currency must not reach the pages that format money");
    }

    /// <summary>
    /// The web app formats every amount from the currency carried in the session cookie,
    /// which is populated from this field. If login stops returning it, the whole app
    /// silently falls back to GBP.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsTheBusinessCurrency()
    {
        var email = $"biz-login-{Guid.NewGuid():N}@example.com";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = "Login Currency Garage",
            ownerName = "Owner",
            email,
            password = Password
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.EmailVerified = true;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var payload = await login.Content.ReadFromJsonAsync<LoginPayload>();

        payload!.User.Currency.Should().Be("GBP", "new businesses default to GBP");
    }

    /// <summary>
    /// Changing the currency has to reach the session without a re-login — the settings
    /// page calls refresh straight after saving for exactly this reason.
    /// </summary>
    [Fact]
    public async Task Refresh_ReturnsTheUpdatedCurrency()
    {
        var client = await CreateTenantAsync("refresh");

        await client.PutAsJsonAsync("/api/business", UpdateBody("USD"));

        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await refresh.Content.ReadFromJsonAsync<LoginPayload>();
        payload!.User.Currency.Should().Be("USD");
    }
}
