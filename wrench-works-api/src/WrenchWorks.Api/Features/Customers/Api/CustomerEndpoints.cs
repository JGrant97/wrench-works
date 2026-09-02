namespace WrenchWorks.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers").RequireAuthorization();

        // Optional query parameters keep their defaults: a required parameter that fails to
        // bind throws, and ErrorHandlingMiddleware turns that into a 500 rather than a 400.
        group.MapGet("/",
            (ICustomerEndpointHandler handler, CancellationToken ct,
             int page = 1, int pageSize = 25, string? search = null, bool includeArchived = false) =>
                handler.ListAsync(page, pageSize, search, includeArchived, ct))
            .RequireAuthorization("customers.view");

        group.MapGet("/{id:guid}",
            (Guid id, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.GetAsync(id, ct))
            .RequireAuthorization("customers.view");

        group.MapPost("/",
            (CreateCustomerRequest request, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.CreateAsync(request, ct))
            .RequireAuthorization("customers.manage");

        group.MapPut("/{id:guid}",
            (Guid id, UpdateCustomerRequest request, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateAsync(id, request, ct))
            .RequireAuthorization("customers.manage");

        group.MapGet("/search",
            (string q, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.SearchAsync(q, ct))
            .RequireAuthorization("customers.view");

        group.MapDelete("/{id:guid}",
            (Guid id, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteAsync(id, ct))
            .RequireAuthorization("customers.manage");

        group.MapPost("/{id:guid}/archive",
            (Guid id, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.ArchiveAsync(id, ct))
            .RequireAuthorization("customers.manage");

        group.MapPost("/{id:guid}/unarchive",
            (Guid id, ICustomerEndpointHandler handler, CancellationToken ct) =>
                handler.UnarchiveAsync(id, ct))
            .RequireAuthorization("customers.manage");
    }
}
