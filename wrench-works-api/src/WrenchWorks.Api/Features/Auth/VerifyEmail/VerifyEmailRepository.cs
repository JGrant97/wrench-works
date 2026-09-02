using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public class VerifyEmailRepository(AppDbContext db) : IVerifyEmailRepository
{
    public Task<User?> FindUserByVerificationTokenAsync(string token, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct);

    public Task<List<BusinessUser>> GetPendingMembershipsAsync(Guid userId, CancellationToken ct) =>
        db.BusinessUsers
          .IgnoreQueryFilters()
          .Where(bu => bu.UserId == userId && bu.Status == BusinessUserStatus.Pending)
          .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
