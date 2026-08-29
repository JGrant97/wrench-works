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
    /// DEFECT PINNED: /api/users/me returns 401 for everyone, including an Admin.
    ///
    /// JwtTokenService emits a "sub" claim, but the JwtBearer handler remaps inbound
    /// standard claims by default, so "sub" arrives as ClaimTypes.NameIdentifier and
    /// CurrentUserService.UserId — which reads FindFirstValue("sub") — is always null.
    /// RequireUserId() then throws UnauthorizedAccessException, which the error
    /// middleware maps to 401.
    ///
    /// Custom claim names (business_id, business_user_id, permission, feature) are not
    /// remapped, which is why tenancy and permissions work and this went unnoticed.
    ///
    /// FIX: options.MapInboundClaims = false in the JwtBearer setup in Program.cs
    /// (or read ClaimTypes.NameIdentifier in CurrentUserService).
    /// When fixed, this should be 200 — update the assertion and docs/app-flow.md.
    /// </summary>
    [Fact]
    public async Task GetMe_Returns401_BecauseTheSubClaimIsRemapped()
    {
        var owner = await CreateBusinessWithOwnerAsync("b");

        var response = await owner.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "CurrentUserService.UserId is always null, so RequireUserId() throws");
    }

    /// <summary>
    /// DEFECT PINNED: an invited user can never log in.
    ///
    /// InviteAsync creates the membership with Status = Pending. LoginEndpoint only
    /// loads BusinessUsers where Status == Active and returns 403 "No active business
    /// membership" when none is found. Nothing in the API or the UI transitions a
    /// membership from Pending to Active, so the invite flow is a dead end.
    ///
    /// FIX: an activation path — accept-invite endpoint, or set Active on invite.
    /// When fixed, the login below should succeed.
    /// </summary>
    [Fact]
    public async Task InvitedUser_CannotLogIn_BecauseMembershipStaysPending()
    {
        var owner = await CreateBusinessWithOwnerAsync("c");
        var email = $"invited-{Guid.NewGuid():N}@example.com";

        var invite = await owner.PostAsJsonAsync("/api/users/invite", new
        {
            name = "Invited Member",
            email,
            roleName = "ReadOnly"
        });
        invite.StatusCode.Should().Be(HttpStatusCode.Created, "the invite itself succeeds");

        // Give the invited account a known password and a verified email, so the ONLY
        // remaining obstacle is the Pending membership.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            user.EmailVerified = true;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        login.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the membership is Pending and nothing can activate it");
    }

    /// <summary>
    /// DEFECT PINNED: every CreatedByUserId audit column is written as null.
    ///
    /// Same root cause as GetMe above — currentUser.UserId is always null, and it is
    /// what stamps Job.CreatedByUserId, Booking.CreatedByUserId and
    /// StockMovement.CreatedByUserId. Confirmed against the dev database: 8/8 bookings,
    /// 8/8 jobs and 4/4 stock movements had a null CreatedByUserId.
    /// </summary>
    [Fact]
    public async Task CreatedByUserId_IsNull_BecauseUserIdClaimIsUnreadable()
    {
        var owner = await CreateBusinessWithOwnerAsync("d");

        var customer = await owner.PostAsJsonAsync("/api/customers", new { name = "Audit Test", phone = "0100000000" });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customer.Content.ReadFromJsonAsync<IdOnly>())!.Id;

        var vehicle = await owner.PostAsJsonAsync("/api/vehicles", new { customerId, make = "Ford", model = "Focus" });
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

        created.CreatedByUserId.Should().BeNull(
            "the sub claim is remapped, so no user id ever reaches the audit column");
    }

    private sealed record IdOnly(Guid Id);
}
