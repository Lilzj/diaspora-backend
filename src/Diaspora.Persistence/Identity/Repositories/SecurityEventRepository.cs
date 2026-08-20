using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <inheritdoc cref="ISecurityEventRepository" />
public sealed class SecurityEventRepository : ISecurityEventRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SecurityEventRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<SecurityEvent> Events => _dbContext.Set<SecurityEvent>();

    public async Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default) =>
        await Events.AddAsync(securityEvent, cancellationToken);

    public async Task<IReadOnlyList<SecurityEvent>> ListByUserAsync(
        UserId userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await Events
            .AsNoTracking()
            .Where(securityEvent => securityEvent.UserId == userId)
            .OrderByDescending(securityEvent => securityEvent.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByTypeSinceAsync(
        UserId userId,
        SecurityEventType type,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await Events.CountAsync(
            securityEvent =>
                securityEvent.UserId == userId &&
                securityEvent.Type == type &&
                securityEvent.OccurredAt >= since,
            cancellationToken);
}
