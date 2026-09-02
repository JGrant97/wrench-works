using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Messaging;

public class MessagingRepository(AppDbContext db) : IMessagingRepository
{
    // Paged in the database. The envelope is generic, so it carries entities here and
    // MessageDto after the handler maps it -- the count and page maths happen once.
    public async Task<PagedResult<OutboundMessage>> ListAsync(
        Guid? customerId, Guid? jobId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.OutboundMessages.AsQueryable();
        if (customerId.HasValue) query = query.Where(m => m.CustomerId == customerId);
        if (jobId.HasValue) query = query.Where(m => m.JobId == jobId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OutboundMessage>(items, total, page, pageSize);
    }

    public async Task<OutboundMessage?> FindAsync(Guid id, CancellationToken ct) =>
        await db.OutboundMessages.FindAsync([id], ct);

    public void Add(OutboundMessage message) => db.OutboundMessages.Add(message);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
