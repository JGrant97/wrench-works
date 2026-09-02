namespace WrenchWorks.Api.Features.Tax;

public static class TaxEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tax").WithTags("Tax").RequireAuthorization();

        group.MapGet("/rates",
            (ITaxEndpointHandler handler, CancellationToken ct, bool includeArchived = false) =>
                handler.ListAsync(includeArchived, ct))
            .RequireAuthorization("settings.manage");

        group.MapPost("/rates",
            (SaveTaxRateRequest request, ITaxEndpointHandler handler, CancellationToken ct) =>
                handler.CreateAsync(request, ct))
            .RequireAuthorization("settings.manage");

        group.MapPut("/rates/{id:guid}",
            (Guid id, SaveTaxRateRequest request, ITaxEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateAsync(id, request, ct))
            .RequireAuthorization("settings.manage");

        group.MapDelete("/rates/{id:guid}",
            (Guid id, ITaxEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteAsync(id, ct))
            .RequireAuthorization("settings.manage");

        group.MapPost("/rates/{id:guid}/archive",
            (Guid id, ITaxEndpointHandler handler, CancellationToken ct) =>
                handler.ArchiveAsync(id, ct))
            .RequireAuthorization("settings.manage");

        group.MapPost("/rates/{id:guid}/unarchive",
            (Guid id, ITaxEndpointHandler handler, CancellationToken ct) =>
                handler.UnarchiveAsync(id, ct))
            .RequireAuthorization("settings.manage");
    }
}
