using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Users;

// The membership plus the permissions from the current JWT. Permissions are session state
// rather than a stored property of the membership, so they are carried alongside it.
public record CurrentUserProfile(BusinessUser Membership, IEnumerable<string> Permissions);

public interface IUserService
{
    Task<List<BusinessUser>> ListAsync(CancellationToken ct);
    Task<BusinessUser> InviteAsync(InviteUserRequest request, CancellationToken ct);
    Task<CurrentUserProfile> GetMeAsync(CancellationToken ct);
}
