using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Customers;

public class CustomerService(AppDbContext db, CurrentUserService currentUser) : ICustomerService
{
    public async Task<PagedResult<CustomerDto>> ListAsync(int page = 1, int pageSize = 25, string? search = null, bool includeArchived = false, CancellationToken ct = default)
    {
        // Archived customers stay out of lists and pickers but remain resolvable by id,
        // so a historical job still renders the name of the customer it was for.
        var query = db.Customers.AsQueryable();
        if (!includeArchived) query = query.Where(c => c.ArchivedAtUtc == null);

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

        return new PagedResult<CustomerDto>(items, total, page, pageSize);
    }

    public async Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct)
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

        return new CustomerDetailDto(
            customer.Id, customer.Name, customer.Phone, customer.Email,
            customer.Address, customer.PreferredContactMethod, customer.Notes,
            customer.IsTaxExempt, customer.TaxExemptionReference,
            customer.Vehicles.Select(v => new CustomerVehicleDto(v.Id, v.DisplayName ?? "", v.Year, v.Registration, v.Colour != null ? v.Colour.Name : null)),
            recentJobs,
            customer.CreatedAtUtc);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct)
            ?? throw new NotFoundException("Customer not found");

        Archiving.EnsureDeletable("customer",
            new Dependent("vehicles", await db.Vehicles.CountAsync(v => v.CustomerId == id, ct)),
            new Dependent("jobs", await db.Jobs.CountAsync(j => j.CustomerId == id, ct)),
            new Dependent("bookings", await db.Bookings.CountAsync(b => b.CustomerId == id, ct)));

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return;
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct)
            ?? throw new NotFoundException("Customer not found");

        var result = Archiving.Archive(customer, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct)
            ?? throw new NotFoundException("Customer not found");

        var result = Archiving.Unarchive(customer, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
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

        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.PreferredContactMethod, customer.Notes, 0, customer.CreatedAtUtc);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
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

        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.PreferredContactMethod, customer.Notes, 0, customer.CreatedAtUtc);
    }

    public async Task<List<CustomerSearchResultDto>> SearchAsync(string q, CancellationToken ct)
    {
        var s = q.ToLower();
        var results = await db.Customers
            .Where(c => c.Name.ToLower().Contains(s) ||
                        (c.Phone != null && c.Phone.Contains(s)) ||
                        (c.Email != null && c.Email.ToLower().Contains(s)))
            .Take(20)
            .Select(c => new CustomerSearchResultDto(c.Id, c.Name, c.Phone, c.Email))
            .ToListAsync(ct);

        return results;
    }
}
