using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

// Anonymous endpoint: it runs before any tenant context exists, so these reads are
// necessarily unfiltered. That is the same reason the other auth slices use
// IgnoreQueryFilters -- see the multi-tenancy note in CLAUDE.md.
public interface IVerifyEmailRepository
{
    Task<User?> FindUserByVerificationTokenAsync(string token, CancellationToken ct);
    Task<List<BusinessUser>> GetPendingMembershipsAsync(Guid userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
