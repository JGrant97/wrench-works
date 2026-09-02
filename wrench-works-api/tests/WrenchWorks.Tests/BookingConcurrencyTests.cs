using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Tests;

/// <summary>
/// Double-booking, which was finding 7: the conflict check read the overlapping set and
/// the caller inserted afterwards, so two simultaneous requests both saw a free slot and
/// both committed. With Capacity = 1 that is two cars in one bay.
///
/// These tests fire genuinely parallel requests rather than sequential ones. A sequential
/// test passes against the broken code — the second request sees the first one's row —
/// so it would have proved nothing. The fix is a row lock on the zone, taken inside a
/// transaction that also covers the insert.
/// </summary>
public class BookingConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Test1234!";
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task ConcurrentBookings_ForTheSameSlot_OnlyOneSucceeds()
    {
        var ctx = await SetUpAsync("race", capacity: 1);

        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        var responses = await BookInParallelAsync(ctx, attempts: 6, start, start.AddHours(2));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        created.Should().Be(1, "a capacity-1 bay can hold exactly one booking for a slot");
        conflicts.Should().Be(5, "every loser should get a 409, not a 500 or a silent success");

        // The status codes could be right while the rows are wrong, so assert the table.
        (await CountBookingsAsync(ctx.ZoneId))
            .Should().Be(1, "the database is the thing that must not be double-booked");
    }

    [Fact]
    public async Task SequentialBooking_ForTheSameSlot_IsRejected()
    {
        // The control for the concurrency tests: if plain overlap detection is broken then
        // the parallel tests would "fail the race" for a completely different reason.
        var ctx = await SetUpAsync("sequential", capacity: 1);

        var start = DateTime.UtcNow.Date.AddDays(6).AddHours(9);

        var first = await PostBookingAsync(ctx.Client, ctx.ZoneId, ctx.CustomerId, ctx.VehicleId, start, start.AddHours(2));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await PostBookingAsync(ctx.Client, ctx.ZoneId, ctx.CustomerId, ctx.VehicleId, start, start.AddHours(2));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, "the bay is already taken for that slot");
    }

    [Fact]
    public async Task ConcurrentBookings_RespectZoneCapacity()
    {
        // A capacity-2 bay legitimately takes two cars at once. This is why the fix is a
        // lock and not an exclusion constraint: a constraint forbids ANY overlap.
        var ctx = await SetUpAsync("capacity", capacity: 2);

        var start = DateTime.UtcNow.Date.AddDays(4).AddHours(9);
        var responses = await BookInParallelAsync(ctx, attempts: 6, start, start.AddHours(2));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created)
            .Should().Be(2, "capacity is 2, so exactly two concurrent bookings should win");

        (await CountBookingsAsync(ctx.ZoneId)).Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentBookings_InDifferentZones_AllSucceed()
    {
        // The lock is per zone. If it were global (or a table lock) this would serialise
        // unrelated bays and quietly cost throughput, so it is worth pinning.
        var ctx = await SetUpAsync("parallel", capacity: 1);
        var secondZone = await CreateZoneAsync(ctx.Client, "Ramp B", capacity: 1);

        var start = DateTime.UtcNow.Date.AddDays(5).AddHours(9);

        var responses = await Task.WhenAll(
            PostBookingAsync(ctx.Client, ctx.ZoneId, ctx.CustomerId, ctx.VehicleId, start, start.AddHours(2)),
            PostBookingAsync(ctx.Client, secondZone, ctx.CustomerId, ctx.VehicleId, start, start.AddHours(2)));

        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created),
            "different bays never conflict with each other");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private record TenantContext(HttpClient Client, Guid ZoneId, Guid CustomerId, Guid VehicleId);

    private static async Task<HttpResponseMessage[]> BookInParallelAsync(
        TenantContext ctx, int attempts, DateTime start, DateTime end) =>
        await Task.WhenAll(Enumerable.Range(0, attempts)
            .Select(_ => PostBookingAsync(ctx.Client, ctx.ZoneId, ctx.CustomerId, ctx.VehicleId, start, end))
            .ToArray());

    private static Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client, Guid zoneId, Guid customerId, Guid vehicleId, DateTime start, DateTime end) =>
        client.PostAsJsonAsync("/api/calendar/bookings", new
        {
            zoneId,
            customerId,
            vehicleId,
            title = "Concurrent service",
            startUtc = start,
            endUtc = end,
            createJob = false
        });

    private async Task<int> CountBookingsAsync(Guid zoneId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings
            .IgnoreQueryFilters()
            .CountAsync(b => b.ZoneId == zoneId && b.Status != BookingStatus.Cancelled);
    }

    private async Task<TenantContext> SetUpAsync(string label, int capacity)
    {
        var client = await CreateTenantAsync(label);
        var zoneId = await CreateZoneAsync(client, "Ramp A", capacity);

        var customer = await client.PostAsJsonAsync("/api/customers", new
        {
            name = $"Concurrent Customer {label}",
            phone = $"555{Guid.NewGuid().ToString("N")[..7]}"
        });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customer.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var vehicleId = await CreateVehicleAsync(client, customerId);

        return new TenantContext(client, zoneId, customerId, vehicleId);
    }

    private static async Task<Guid> CreateZoneAsync(HttpClient client, string name, int capacity)
    {
        var response = await client.PostAsJsonAsync("/api/zones", new
        {
            name = $"{name} {Guid.NewGuid().ToString("N")[..6]}",
            color = "#3b82f6",
            capacity
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private static async Task<Guid> CreateVehicleAsync(HttpClient client, Guid customerId)
    {
        // The catalogue is seeded global reference data, so take whatever variant exists
        // rather than hard-coding one the seed may later rename.
        var makes = await client.GetFromJsonAsync<List<IdNamePayload>>("/api/catalogue/makes");
        var models = await client.GetFromJsonAsync<List<IdNamePayload>>(
            $"/api/catalogue/makes/{makes!.First().Id}/models");
        var years = await client.GetFromJsonAsync<List<int>>(
            $"/api/catalogue/models/{models!.First().Id}/years");
        var variants = await client.GetFromJsonAsync<List<IdPayload>>(
            $"/api/catalogue/models/{models.First().Id}/variants?year={years!.First()}");

        var response = await client.PostAsJsonAsync("/api/vehicles", new
        {
            customerId,
            variantId = variants!.First().Id,
            year = years.First(),
            registration = $"CC{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private async Task<HttpClient> CreateTenantAsync(string label)
    {
        var seed = _factory.CreateClient();
        var email = $"booking-{label}-{Guid.NewGuid():N}@example.com";

        var register = await seed.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = $"Booking Garage {label}",
            ownerName = $"Owner {label}",
            email,
            password = Password
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        // Login is blocked until verified and the token only reaches ConsoleEmailSender,
        // so flip the flag directly — the same shortcut the other suites take.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
            user.EmailVerified = true;
            await db.SaveChangesAsync();
        }

        var login = await seed.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var token = (await login.Content.ReadFromJsonAsync<LoginPayload>())!.Token;

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authed;
    }

    private record LoginPayload(string Token);
    private record IdPayload(Guid Id);
    private record IdNamePayload(Guid Id, string Name);
}
