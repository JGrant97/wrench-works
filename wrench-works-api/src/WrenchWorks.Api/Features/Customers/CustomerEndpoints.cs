using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization("customers.view");
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("customers.view");
        group.MapPost("/", CreateAsync).RequireAuthorization("customers.manage");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("customers.manage");
        group.MapGet("/search", SearchAsync).RequireAuthorization("customers.view");
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization("customers.manage");
        group.MapPost("/{id:guid}/archive", ArchiveAsync).RequireAuthorization("customers.manage");
        group.MapPost("/{id:guid}/unarchive", UnarchiveAsync).RequireAuthorization("customers.manage");
    }

    private static async Task<Ok<PagedResult<CustomerDto>>> ListAsync(ICustomerService svc, int page = 1, int pageSize = 25, string? search = null, bool includeArchived = false, CancellationToken ct = default) =>
        TypedResults.Ok(await svc.ListAsync(page, pageSize, search, includeArchived, ct));

    private static async Task<Ok<CustomerDetailDto>> GetAsync(ICustomerService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetAsync(id, ct));

    private static async Task<NoContent> DeleteAsync(ICustomerService svc, Guid id, CancellationToken ct)
    {
        await svc.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ArchiveResultDto>> ArchiveAsync(ICustomerService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.ArchiveAsync(id, ct));

    private static async Task<Ok<ArchiveResultDto>> UnarchiveAsync(ICustomerService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.UnarchiveAsync(id, ct));

    private static async Task<Created<CustomerDto>> CreateAsync(ICustomerService svc, CreateCustomerRequest request, CancellationToken ct)
    {
        var result = await svc.CreateAsync(request, ct);
        return TypedResults.Created($"/api/customers/{result.Id}", result);
    }

    private static async Task<Ok<CustomerDto>> UpdateAsync(ICustomerService svc, Guid id, UpdateCustomerRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateAsync(id, request, ct));

    private static async Task<Ok<List<CustomerSearchResultDto>>> SearchAsync(ICustomerService svc, string q, CancellationToken ct) =>
        TypedResults.Ok(await svc.SearchAsync(q, ct));
}
