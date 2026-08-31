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
/// Users slice: access control, and two confirmed defects pinned as regression tests.
///
/// These assert CURRENT behaviour, including behaviour that is wrong. Each such test
/// says so and names the fix. When someone fixes the underlying bug the test will fail —
/// that is the point. Update the assertion and the note in docs/app-flow.md together.
/// </summary>
public class UserAccessTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public UserAccessTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "SecurePass123!";

    private sealed record LoginPayload(string Token);

    private async Task VerifyEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
        user.EmailVerified = true;
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> CreateBusinessWithOwnerAsync(string label)
    {
        var email = $"{label}-owner-{Guid.NewGuid():N}@example.com";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Garage {label}",
            ownerName = $"Owner {label}",
            email,
            password = Password
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        await VerifyEmailAsync(email);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = (await login.Content.ReadFromJsonAsync<LoginPayload>())!.Token;
        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authed;
    }

    [Fact]
    public async Task Owner_CanListUsers()
    {
        var owner = await CreateBusinessWithOwnerAsync("a");

        var response = await owner.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the owner holds the Admin role");
    }

    /// <summary>
    /// Regression guard for the "sub" claim.
    ///
    /// The JwtBearer handler remaps inbound standard claims unless told not to, so "sub"
    /// used to arrive as ClaimTypes.NameIdentifier and CurrentUserService.UserId — which
    /// reads FindFirstValue("sub") — was always null. RequireUserId() threw, and /me
    /// returned 401 for everyone including admins. Fixed by MapInboundClaims = false.
    ///
    /// If this starts failing with 401, that setting has been lost.
    /// </summary>
    [Fact]
    public async Task GetMe_ReturnsTheCallersProfile()
    {
        var owner = await CreateBusinessWithOwnerAsync("b");

        var response = await owner.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the sub claim must reach CurrentUserService.UserId");
    }

    /// <summary>
    /// "/me" must not require users.manage — it was declared inside a group carrying that
    /// policy, so only admins could read their own profile.
    /// </summary>
    [Fact]
    public async Task GetMe_IsReadableByANonAdmin()
    {
        var owner = await CreateBusinessWithOwnerAsync("e");
        var email = $"member-{Guid.NewGuid():N}@example.com";

        var invite = await owner.PostAsJsonAsync("/api/users/invite", new
        {
            name = "ReadOnly Member",
            email,
            roleName = "ReadOnly"
        });
        invite.StatusCode.Should().Be(HttpStatusCode.Created);

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            token = user.EmailVerificationToken!;
            await db.SaveChangesAsync();
        }

        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var memberToken = (await login.Content.ReadFromJsonAsync<LoginPayload>())!.Token;

        var member = _factory.CreateClient();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        (await member.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "listing the team still requires users.manage");

        (await member.GetAsync("/api/users/me")).StatusCode.Should().Be(HttpStatusCode.OK,
            "but anyone may read their own profile");
    }

    /// <summary>
    /// The invite flow works end to end, PROVIDED the invitee verifies their email.
    ///
    /// InviteAsync creates the membership as Pending and LoginEndpoint requires Active,
    /// so it looks like a dead end — but VerifyEmailEndpoint activates every pending
    /// membership for the user as part of verification. Verification is the gate, which
    /// is why the invite email carries both the temporary password and the token.
    ///
    /// An earlier version of this test set EmailVerified directly in the database,
    /// skipping the endpoint that performs the activation, and therefore "proved" a
    /// defect that does not exist. Exercise the real endpoint.
    /// </summary>
    [Fact]
    public async Task InvitedUser_CanLogIn_AfterVerifyingTheirEmail()
    {
        var owner = await CreateBusinessWithOwnerAsync("c");
        var email = $"invited-{Guid.NewGuid():N}@example.com";

        var invite = await owner.PostAsJsonAsync("/api/users/invite", new
        {
            name = "Invited Member",
            email,
            roleName = "ReadOnly"
        });
        invite.StatusCode.Should().Be(HttpStatusCode.Created);

        // The invite emails a random temporary password we cannot read, so set a known
        // one. Crucially we do NOT touch EmailVerified or the membership status — those
        // are what the endpoint under test is responsible for.
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            token = user.EmailVerificationToken!;
            await db.SaveChangesAsync();
        }

        token.Should().NotBeNullOrEmpty("the invite issues a verification token");

        // Before verification the membership is Pending, so login is refused.
        var early = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        early.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the email is not verified yet");

        var verify = await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            "verifying the email activates the pending membership");
    }

    /// <summary>
    /// Audit columns must record who acted.
    ///
    /// Same root cause as GetMe: currentUser.UserId stamps Job.CreatedByUserId,
    /// Booking.CreatedByUserId and StockMovement.CreatedByUserId, and while the "sub"
    /// claim was unreadable every one of those was written null — verified across all
    /// 20 rows of the dev database before the fix.
    /// </summary>
    [Fact]
    public async Task CreatedByUserId_IsStamped_OnANewJob()
    {
        var owner = await CreateBusinessWithOwnerAsync("d");

        var customer = await owner.PostAsJsonAsync("/api/customers", new { name = "Audit Test", phone = "0100000000" });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customer.Content.ReadFromJsonAsync<IdOnly>())!.Id;

        // Vehicles are catalogue-backed: pick any seeded variant.
        var makes = await owner.GetFromJsonAsync<List<CatalogueEntry>>("/api/catalogue/makes") ?? [];
        makes.Should().NotBeEmpty("VehicleCatalogueSeeder should have run at startup");

        Guid variantId = Guid.Empty;
        var year = 0;
        foreach (var make in makes)
        {
            var models = await owner.GetFromJsonAsync<List<CatalogueEntry>>($"/api/catalogue/makes/{make.Id}/models") ?? [];
            foreach (var model in models)
            {
                var years = await owner.GetFromJsonAsync<List<int>>($"/api/catalogue/models/{model.Id}/years") ?? [];
                if (years.Count == 0) continue;
                var variants = await owner.GetFromJsonAsync<List<CatalogueEntry>>(
                    $"/api/catalogue/models/{model.Id}/variants?year={years[0]}") ?? [];
                if (variants.Count == 0) continue;
                variantId = variants[0].Id;
                year = years[0];
                break;
            }
            if (variantId != Guid.Empty) break;
        }
        variantId.Should().NotBe(Guid.Empty, "the seeded catalogue should contain at least one variant");

        var vehicle = await owner.PostAsJsonAsync("/api/vehicles", new { customerId, variantId, year });
        vehicle.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicleId = (await vehicle.Content.ReadFromJsonAsync<IdOnly>())!.Id;

        var job = await owner.PostAsJsonAsync("/api/jobs", new
        {
            customerId,
            vehicleId,
            title = "Audit stamp check",
            priority = "Normal"
        });
        job.StatusCode.Should().Be(HttpStatusCode.Created);
        var jobId = (await job.Content.ReadFromJsonAsync<IdOnly>())!.Id;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await db.Jobs.IgnoreQueryFilters().SingleAsync(j => j.Id == jobId);

        created.CreatedByUserId.Should().NotBeNull(
            "the acting user's id must reach the audit column");
    }

    private sealed record IdOnly(Guid Id);
    private sealed record CatalogueEntry(Guid Id, string Name);
}
