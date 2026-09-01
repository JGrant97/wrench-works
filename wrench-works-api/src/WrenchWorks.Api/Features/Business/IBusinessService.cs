namespace WrenchWorks.Api.Features.Business;

// The Business slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IBusinessService
{
    Task<BusinessDto> GetAsync(CancellationToken ct);
    Task<BusinessDto> UpdateAsync(UpdateBusinessRequest request, CancellationToken ct);
}
