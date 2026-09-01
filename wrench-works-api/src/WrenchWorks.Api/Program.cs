using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Auth.Login;
using WrenchWorks.Api.Features.Auth.Register;
using WrenchWorks.Api.Features.Auth.RefreshToken;
using WrenchWorks.Api.Features.Auth.VerifyEmail;
using WrenchWorks.Api.Features.Billing;
using WrenchWorks.Api.Features.Business;
using WrenchWorks.Api.Features.Calendar;
using WrenchWorks.Api.Features.Catalogue;
using WrenchWorks.Api.Features.Customers;
using WrenchWorks.Api.Features.Dashboard;
using WrenchWorks.Api.Features.Inventory;
using WrenchWorks.Api.Features.Jobs;
using WrenchWorks.Api.Features.Messaging;
using WrenchWorks.Api.Features.Tax;
using WrenchWorks.Api.Features.Users;
using WrenchWorks.Api.Features.Vehicles;
using WrenchWorks.Api.Features.Zones;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;
using WrenchWorks.Infrastructure.Stripe;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────── Database ────────────────────
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// ──────────────────── Auth ────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep JWT claim names exactly as issued. By default the handler remaps standard
        // claims — "sub" would arrive as ClaimTypes.NameIdentifier, so CurrentUserService's
        // FindFirstValue("sub") returned null for every request. That silently broke
        // /api/users/me (401 for everyone) and left every CreatedByUserId audit column
        // unwritten. The custom claims (business_id, permission, feature) were never
        // remapped, which is why tenancy and authorization worked and this went unnoticed.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPermissionPolicies();
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ──────────────────── Services ────────────────────
builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
builder.Services.AddScoped<ISmsSender, ConsoleSmsSender>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ──────────────────── CORS ────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ──────────────────── OpenAPI ────────────────────
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "Wrench Works API";
        document.Info.Version = "v1";
        document.Info.Description = "Workshop management SaaS API";
        return Task.CompletedTask;
    });

    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

// ──────────────────── Middleware ────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ──────────────────── OpenAPI + Scalar ────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Wrench Works API")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
}

// ──────────────────── Map Feature Endpoints ────────────────────
RegisterEndpoint.Map(app);
LoginEndpoint.Map(app);
VerifyEmailEndpoint.Map(app);
RefreshTokenEndpoint.Map(app);
BusinessEndpoints.Map(app);
UserEndpoints.Map(app);
ZoneEndpoints.Map(app);
CustomerEndpoints.Map(app);
CatalogueEndpoints.Map(app);
VehicleEndpoints.Map(app);
CalendarEndpoints.Map(app);
JobEndpoints.Map(app);
DashboardEndpoints.Map(app);
TaxEndpoints.Map(app);
InventoryEndpoints.Map(app);
MessagingEndpoints.Map(app);
BillingEndpoints.Map(app);

// ──────────────────── Health check ────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous()
   .WithTags("System");

// ──────────────────── DB Migration & Seed ────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await PermissionSeeder.SeedPermissionsAsync(db);
    await VehicleCatalogueSeeder.SeedAsync(db);
}

app.Run();

public partial class Program { } // For integration test WebApplicationFactory
