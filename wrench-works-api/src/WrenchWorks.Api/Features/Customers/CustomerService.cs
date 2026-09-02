using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Customers;

public class CustomerService(ICustomerRepository repository, CurrentUserService currentUser) : ICustomerService
{
    public Task<PagedResult<CustomerWithVehicleCount>> ListAsync(
        int page, int pageSize, string? search, bool includeArchived, CancellationToken ct) =>
        repository.ListAsync(page, pageSize, search, includeArchived, ct);

    public async Task<CustomerDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.FindWithVehiclesAsync(id, ct)
            ?? throw new NotFoundException("Customer not found");

        return new CustomerDetail(customer, await repository.GetRecentJobsAsync(id, 10, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Customer not found");

        Archiving.EnsureDeletable("customer",
            new Dependent("vehicles", await repository.CountVehiclesAsync(id, ct)),
            new Dependent("jobs", await repository.CountJobsAsync(id, ct)),
            new Dependent("bookings", await repository.CountBookingsAsync(id, ct)));

        repository.Remove(customer);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Customer not found");

        var result = Archiving.Archive(customer, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Customer not found");

        var result = Archiving.Unarchive(customer, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<Customer> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        await new CreateCustomerValidator().ValidateAndThrowAsync(request, ct);

        // Duplicate warning (phone or email)
        if (!string.IsNullOrEmpty(request.Phone) && await repository.PhoneExistsAsync(request.Phone, ct))
            throw new ConflictException("A customer with this phone number already exists");

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

        repository.Add(customer);
        await repository.SaveChangesAsync(ct);
        return customer;
    }

    // No validation here, unlike CreateAsync which enforces name and email. Pre-existing
    // and still open -- finding 18 in docs/review-findings.md.
    public async Task<Customer> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Customer not found");

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.PreferredContactMethod = request.PreferredContactMethod;
        customer.Notes = request.Notes;

        await repository.SaveChangesAsync(ct);
        return customer;
    }

    public Task<List<Customer>> SearchAsync(string q, CancellationToken ct) =>
        repository.SearchAsync(q, 20, ct);
}
