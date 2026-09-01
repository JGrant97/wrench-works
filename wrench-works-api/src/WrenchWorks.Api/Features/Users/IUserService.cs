namespace WrenchWorks.Api.Features.Users;

// The User slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IUserService
{
    Task<List<UserListItemDto>> ListAsync(CancellationToken ct);
    Task<InvitedUserDto> InviteAsync(InviteUserRequest request, CancellationToken ct);
    Task<CurrentUserDto> GetMeAsync(CancellationToken ct);
}
