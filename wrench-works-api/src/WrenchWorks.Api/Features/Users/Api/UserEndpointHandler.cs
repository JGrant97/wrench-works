using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Users;

public class UserEndpointHandler(IUserService service) : IUserEndpointHandler
{
    public async Task<Ok<List<UserListItemDto>>> ListAsync(CancellationToken ct)
    {
        var members = await service.ListAsync(ct);
        return TypedResults.Ok(members.Select(bu => new UserListItemDto(
            bu.UserId, bu.Id, bu.User.Name, bu.User.Email, bu.Status.ToString(),
            bu.Roles.Select(r => r.Role.Name), bu.CreatedAtUtc)).ToList());
    }

    public async Task<Created<InvitedUserDto>> InviteAsync(InviteUserRequest request, CancellationToken ct)
    {
        var membership = await service.InviteAsync(request, ct);
        return TypedResults.Created($"/api/users/{membership.Id}",
            new InvitedUserDto(membership.Id, membership.User.Name, membership.User.Email,
                membership.Status.ToString()));
    }

    public async Task<Ok<CurrentUserDto>> GetMeAsync(CancellationToken ct)
    {
        var profile = await service.GetMeAsync(ct);
        var bu = profile.Membership;
        return TypedResults.Ok(new CurrentUserDto(
            bu.UserId, bu.User.Name, bu.User.Email, bu.BusinessId, bu.Business.Name,
            bu.Roles.Select(r => r.Role.Name), profile.Permissions));
    }
}
