using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Users;

public interface IUserEndpointHandler
{
    Task<Ok<List<UserListItemDto>>> ListAsync(CancellationToken ct);
    Task<Created<InvitedUserDto>> InviteAsync(InviteUserRequest request, CancellationToken ct);
    Task<Ok<CurrentUserDto>> GetMeAsync(CancellationToken ct);
}
