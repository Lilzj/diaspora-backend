using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for placements.
/// <para>
/// <b>The batch lookup is the one that matters.</b> A list page resolves fifty subjects at once, and
/// a per-subject query would put fifty round trips on somebody's scroll. Everything else here is a
/// sweep or an operator surface.
/// </para>
/// </summary>
public sealed class PlacementRepository : IPlacementRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PlacementRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Placement> Placements => _dbContext.Set<Placement>();

    public void Add(Placement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        Placements.Add(placement);
    }

    public void Update(Placement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        FinancialAggregateUpdater.MarkDirty(_dbContext, placement);
    }

    public async Task<Placement?> FindAsync(
        PlacementId placementId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placementId);

        return await Placements.FirstOrDefaultAsync(
            placement => placement.Id == placementId, cancellationToken);
    }

    public async Task<IReadOnlyList<Placement>> ListActiveForSubjectAsync(
        SubjectReference subject, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var subjectType = subject.SubjectType;
        var subjectId = subject.SubjectId;

        return await Placements
            .Where(placement => placement.Subject.SubjectType == subjectType
                && placement.Subject.SubjectId == subjectId
                && placement.Status == PlacementStatus.Active
                && placement.Window.StartsAt <= asOf
                && (placement.Window.EndsAt == null || placement.Window.EndsAt > asOf))
            .OrderByDescending(placement => placement.PriorityWeight)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Placement>> ListActiveForSubjectsAsync(
        MonetizationSubjectType subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectIds);

        if (subjectIds.Count == 0)
        {
            return [];
        }

        var wanted = subjectIds.ToList();

        return await Placements
            .Where(placement => placement.Subject.SubjectType == subjectType
                && wanted.Contains(placement.Subject.SubjectId)
                && placement.Status == PlacementStatus.Active
                && placement.Window.StartsAt <= asOf
                && (placement.Window.EndsAt == null || placement.Window.EndsAt > asOf))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A member's placements, cursor-paged.
    /// <para>
    /// <b>The cursor excludes by identity rather than by strict inequality on the timestamp.</b> Two
    /// placements bought in one request share a <c>CreatedAt</c> to the tick, and a
    /// <c>&lt; before</c> cursor drops whichever one did not fit on the page — silently, for ever.
    /// ADR-017.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Placement>> ListByPurchaserAsync(
        Guid userId,
        PlacementStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        var placements = Placements.Where(placement => placement.PurchasedByUserId == userId);

        if (status is { } wanted)
        {
            placements = placements.Where(placement => placement.Status == wanted);
        }

        if (before is { } boundary)
        {
            placements = placements.Where(placement => placement.CreatedAt <= boundary);
        }

        if (excludeIds is { Count: > 0 })
        {
            // Compared at the value-object level. Reaching through a value-converted identifier to
            // its `.Value` compiles and throws at runtime — the platform's LINQ trap, recorded twice.
            var excluded = excludeIds.Select(PlacementId.From).ToList();

            placements = placements.Where(placement => !excluded.Contains(placement.Id));
        }

        return await placements
            .OrderByDescending(placement => placement.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveAsync(
        DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        Placements.CountAsync(
            placement => placement.Status == PlacementStatus.Active
                && placement.Window.StartsAt <= asOf
                && (placement.Window.EndsAt == null || placement.Window.EndsAt > asOf),
            cancellationToken);

    public async Task<IReadOnlyList<Placement>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Placements
            .Where(placement =>
                (placement.Status == PlacementStatus.Active
                    || placement.Status == PlacementStatus.Scheduled)
                && placement.Window.EndsAt != null
                && placement.Window.EndsAt <= asOf)
            .OrderBy(placement => placement.Window.EndsAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Placement>> ListDueAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Placements
            .Where(placement => placement.Status == PlacementStatus.Scheduled
                && placement.Window.StartsAt <= asOf)
            .OrderBy(placement => placement.Window.StartsAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Placement>> ListExpiringAsync(
        DateTimeOffset asOf,
        DateTimeOffset until,
        int take,
        CancellationToken cancellationToken = default) =>
        await Placements
            .Where(placement =>
                (placement.Status == PlacementStatus.Active
                    || placement.Status == PlacementStatus.Scheduled)
                && placement.Window.EndsAt != null
                && placement.Window.EndsAt > asOf
                && placement.Window.EndsAt <= until
                && (placement.ExpiryAnnouncedFor == null
                    || placement.ExpiryAnnouncedFor != placement.Window.EndsAt))
            .OrderBy(placement => placement.Window.EndsAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<bool> HasOverlapAsync(
        SubjectReference subject,
        PlacementKind kind,
        DateWindow window,
        PlacementId? excluding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(window);

        var subjectType = subject.SubjectType;
        var subjectId = subject.SubjectId;
        var startsAt = window.StartsAt;
        var endsAt = window.EndsAt;

        var candidates = Placements.Where(placement =>
            placement.Subject.SubjectType == subjectType
            && placement.Subject.SubjectId == subjectId
            && placement.Kind == kind
            && (placement.Status == PlacementStatus.Active
                || placement.Status == PlacementStatus.Scheduled
                || placement.Status == PlacementStatus.Paused));

        if (excluding is not null)
        {
            candidates = candidates.Where(placement => placement.Id != excluding);
        }

        // Two windows overlap unless one ends before the other starts. An open-ended window
        // (EndsAt == null) overlaps everything after its start.
        return await candidates.AnyAsync(
            placement => (endsAt == null || placement.Window.StartsAt < endsAt)
                && (placement.Window.EndsAt == null || placement.Window.EndsAt > startsAt),
            cancellationToken);
    }
}
