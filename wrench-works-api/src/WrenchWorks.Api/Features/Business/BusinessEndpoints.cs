using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Business;

/// <summary>
/// The currencies the product supports. A closed set rather than free text: every amount
/// in the app is formatted from this value, so an unrecognised code would render as the
/// raw string next to the number and there would be no way to spot it from the server.
///
/// The UI offers exactly these; this is the check that makes the dropdown non-negotiable,
/// since a request need not come from the dropdown.
/// </summary>
public static class SupportedCurrencies
{
    public static readonly string[] Codes = ["GBP", "USD", "EUR"];

    public static bool IsSupported(string? code) =>
        code is not null && Codes.Contains(code, StringComparer.OrdinalIgnoreCase);
}

public static class BusinessEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/business").WithTags("Business").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPut("/", UpdateAsync).RequireAuthorization("settings.manage");
    }

    private static async Task<Ok<BusinessDto>> GetAsync(IBusinessService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetAsync(ct));

    private static async Task<Ok<BusinessDto>> UpdateAsync(IBusinessService svc, UpdateBusinessRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateAsync(request, ct));
}
