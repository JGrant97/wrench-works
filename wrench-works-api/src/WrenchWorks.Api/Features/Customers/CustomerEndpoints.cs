using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Customers;

// DTOs
public record CreateCustomerRequest(string Name, string? Phone, string? Email, string? Address, string? PreferredContactMethod, string? Notes);
public record UpdateCustomerRequest(string Name, string? Phone, string? Email, string? Address, string? PreferredContactMethod, string? Notes);
public record CustomerDto(Guid Id, string Name, string? Phone, string? Email, string? Address, string? PreferredContactMethod, string? Notes, int VehicleCount, DateTime CreatedAtUtc);
public record CustomerDetailDto(Guid Id, string Name, string? Phone, string? Email, string? Address, string? PreferredContactMethod, string? Notes, IEnumerable<CustomerVehicleDto> Vehicles, IEnumerable<CustomerJobDto> RecentJobs, DateTime CreatedAtUtc);
public record CustomerVehicleDto(Guid Id, string DisplayName, int? Year, string? Registration, string? ColourName);
public record CustomerJobDto(Guid Id, string Title, string Status, string? VehicleDisplay, decimal Total, DateTime CreatedAtUtc);

// Validators
public class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

// Endpoints
public static class CustomerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization("customers.view").Produces<PagedResult<CustomerDto>>();
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("customers.view").Produces<CustomerDetailDto>();
        group.MapPost("/", CreateAsync).RequireAuthorization("customers.manage");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("customers.manage");
        group.MapGet("/search", SearchAsync).RequireAuthorization("customers.view");
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        int page = 1,
        int pageSize = 25,
        string? search = null,
        CancellationToken ct = default)
    {
        var query = db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Email, c.Address, c.PreferredContactMethod, c.Notes, c.Vehicles.Count, c.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<CustomerDto>(items, total, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Vehicles).ThenInclude(v => v.Colour)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Customer not found");

        // The page has always rendered a "Recent Jobs" card; the DTO never carried the
        // data, so it was permanently empty even for customers with a full history.
        // Queried separately rather than Include'd: a customer's whole job history with
        // line items would be a large graph to materialise just to show the last few.
        var recentJobs = await db.Jobs
            .Where(j => j.CustomerId == id)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(10)
            .Select(j => new CustomerJobDto(
                j.Id,
                j.Title,
                j.Status.ToString(),
                j.Vehicle.DisplayName,
                j.LaborLines.Sum(l => l.Hours * l.Rate) + j.PartLines.Sum(p => p.Quantity * p.UnitPrice),
                j.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new CustomerDetailDto(
            customer.Id, customer.Name, customer.Phone, customer.Email,
            customer.Address, customer.PreferredContactMethod, customer.Notes,
            customer.Vehicles.Select(v => new CustomerVehicleDto(v.Id, v.DisplayName ?? "", v.Year, v.Registration, v.Colour != null ? v.Colour.Name : null)),
            recentJobs,
            customer.CreatedAtUtc));
    }

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        await new CreateCustomerValidator().ValidateAndThrowAsync(request, ct);

        // Duplicate warning (phone or email)
        if (!string.IsNullOrEmpty(request.Phone))
        {
            var phoneExists = await db.Customers.AnyAsync(c => c.Phone == request.Phone, ct);
            if (phoneExists)
                throw new ConflictException("A customer with this phone number already exists");
        }

        var customer = new Customer
        {
            BusinessId = currentUser.RequireBusinessId(),
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            PreferredContactMethod = request.PreferredContactMethod,
            Notes = request.Notes
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/customers/{customer.Id}",
            new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.PreferredContactMethod, customer.Notes, 0, customer.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateCustomerRequest request, AppDbContext db, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct)
            ?? throw new NotFoundException("Customer not found");

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.PreferredContactMethod = request.PreferredContactMethod;
        customer.Notes = request.Notes;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.PreferredContactMethod, customer.Notes, 0, customer.CreatedAtUtc));
    }

    private static async Task<IResult> SearchAsync(AppDbContext db, string q, CancellationToken ct)
    {
        var s = q.ToLower();
        var results = await db.Customers
            .Where(c => c.Name.ToLower().Contains(s) ||
                        (c.Phone != null && c.Phone.Contains(s)) ||
                        (c.Email != null && c.Email.ToLower().Contains(s)))
            .Take(20)
            .Select(c => new { c.Id, c.Name, c.Phone, c.Email })
            .ToListAsync(ct);

        return Results.Ok(results);
    }
}
