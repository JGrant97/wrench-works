using System.Net.Http.Json;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Register test database
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public AuthTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Register_ValidInput_ReturnsCreated()
    {
        var payload = new
        {
            businessName = "Test Garage",
            ownerName = "John Doe",
            email = $"test-{Guid.NewGuid():N}@example.com",
            password = "SecurePass123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnverifiedEmail_Returns403()
    {
        var email = $"test-{Guid.NewGuid():N}@example.com";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            businessName = "Test Garage",
            ownerName = "Jane Doe",
            email,
            password = "SecurePass123!"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "SecurePass123!"
        });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var payload = new
        {
            businessName = "Garage A",
            ownerName = "Owner A",
            email,
            password = "SecurePass123!"
        };

        await _client.PostAsJsonAsync("/api/auth/register", payload);
        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }
}
