using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Customers;

public class CustomerEndpointHandler(ICustomerService service) : ICustomerEndpointHandler
{
    private static CustomerDto ToDto(Customer c, int vehicleCount) =>
        new(c.Id, c.Name, c.Phone, c.Email, c.Address, c.PreferredContactMethod, c.Notes,
            vehicleCount, c.CreatedAtUtc);

    public async Task<Ok<PagedResult<CustomerDto>>> ListAsync(
        int page, int pageSize, string? search, bool includeArchived, CancellationToken ct)
    {
        var result = await service.ListAsync(page, pageSize, search, includeArchived, ct);
        return TypedResults.Ok(new PagedResult<CustomerDto>(
            result.Items.Select(r => ToDto(r.Customer, r.VehicleCount)).ToList(),
            result.Total, result.Page, result.PageSize));
    }

    public async Task<Ok<CustomerDetailDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var detail = await service.GetAsync(id, ct);
        var c = detail.Customer;

        return TypedResults.Ok(new CustomerDetailDto(
            c.Id, c.Name, c.Phone, c.Email, c.Address, c.PreferredContactMethod, c.Notes,
            c.IsTaxExempt, c.TaxExemptionReference,
            c.Vehicles.Select(v => new CustomerVehicleDto(
                v.Id, v.DisplayName ?? "", v.Year, v.Registration,
                v.Colour != null ? v.Colour.Name : null)),
            detail.RecentJobs.Select(j => new CustomerJobDto(
                j.Id, j.Title, j.Status.ToString(), j.VehicleDisplay, j.Total, j.CreatedAtUtc)),
            c.CreatedAtUtc));
    }

    public async Task<NoContent> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    public async Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.ArchiveAsync(id, ct));

    public async Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.UnarchiveAsync(id, ct));

    // vehicleCount is 0 on create and update, as it was before this refactor: a new
    // customer genuinely has none, and update does not re-count. Returning the real count
    // on update would be a contract change, so it is left alone rather than quietly fixed.
    public async Task<Created<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/customers/{customer.Id}", ToDto(customer, 0));
    }

    public async Task<Ok<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.UpdateAsync(id, request, ct), 0));

    public async Task<Ok<List<CustomerSearchResultDto>>> SearchAsync(string q, CancellationToken ct)
    {
        var customers = await service.SearchAsync(q, ct);
        return TypedResults.Ok(customers
            .Select(c => new CustomerSearchResultDto(c.Id, c.Name, c.Phone, c.Email))
            .ToList());
    }
}
