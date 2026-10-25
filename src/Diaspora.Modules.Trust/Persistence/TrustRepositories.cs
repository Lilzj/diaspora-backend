using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Diaspora.Persistence.Querying;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Trust.Persistence;

/// <summary>
/// EF Core repositories over the shared <c>DiasporaDbContext</c>.
/// <para>
/// Command paths load tracked and include the child collections the aggregate needs to
/// enforce its invariants — a verification cannot check "have I already accepted this
/// document type?" against a collection EF did not load. Read paths are untracked and load
/// only what the projection renders.
/// </para>
/// </summary>
public sealed class VerificationRequestRepository : IVerificationRequestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VerificationRequestRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<VerificationRequest> Requests => _dbContext.Set<VerificationRequest>();

    public async Task<VerificationRequest?> GetAsync(
        VerificationRequestId id, CancellationToken cancellationToken = default) =>
        await Requests
            .Include(request => request.Documents)
            .Include(request => request.Reviews)
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task<VerificationRequest?> GetOpenByTypeAsync(
        UserId userId, VerificationType type, CancellationToken cancellationToken = default)
    {
        var open = OpenStatuses();

        return await Requests
            .Include(request => request.Documents)
            .FirstOrDefaultAsync(
                request => request.UserId == userId
                    && request.Type == type

                    // Member-scoped rows only. A claim this member raised about a provider they
                    // own is a different claim from one about themselves, and must not block it
                    // — a member who runs a business still has their own identity to verify.
                    // No row written before B3.0.2 has a subject, so this narrows nothing that
                    // existed when the method was written.
                    && request.SubjectType == null
                    && open.Contains(request.Status),
                cancellationToken);
    }

    public async Task<VerificationRequest?> GetOpenBySubjectAsync(
        VerificationType type,
        string subjectType,
        Guid? subjectId,
        CancellationToken cancellationToken = default)
    {
        var open = OpenStatuses();

        return await Requests
            .Include(request => request.Documents)
            .FirstOrDefaultAsync(
                request => request.Type == type
                    && request.SubjectType == subjectType
                    && request.SubjectId == subjectId
                    && open.Contains(request.Status),
                cancellationToken);
    }

    /// <summary>
    /// The idempotency lookup. Deliberately unfiltered by status: a retry must find the original
    /// case even after it has been decided, or a caller replaying a message would open a second
    /// verification for a claim that has already been answered.
    /// </summary>
    public async Task<VerificationRequest?> GetByCorrelationAsync(
        Guid correlationId, CancellationToken cancellationToken = default) =>
        await Requests
            .Include(request => request.Documents)
            .FirstOrDefaultAsync(
                request => request.CorrelationId == correlationId, cancellationToken);

    public async Task<IReadOnlyList<VerificationRequest>> ListGrantedAsync(
        UserId userId, DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        await Requests
            .AsNoTracking()
            .Where(request => request.UserId == userId
                && (request.Status == VerificationStatus.Approved
                    || request.Status == VerificationStatus.PartiallyApproved)
                && (request.ExpiresAt == null || request.ExpiresAt > asOf))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<VerificationRequest>> ListForUserAsync(
        UserId userId,
        PaginationRequest pagination,
        VerificationType? type = null,
        VerificationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var query = Requests
            .AsNoTracking()
            .Where(request => request.UserId == userId);

        if (type is { } requestedType)
        {
            query = query.Where(request => request.Type == requestedType);
        }

        if (status is { } requestedStatus)
        {
            query = query.Where(request => request.Status == requestedStatus);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(request => request.SubmittedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<VerificationRequest>.Create(
            items, pagination.Page, pagination.PageSize, total);
    }

    public async Task<PagedResult<VerificationRequest>> ListQueueAsync(
        PaginationRequest pagination,
        VerificationType? type = null,
        VerificationStatus? status = null,
        UserId? assignedTo = null,
        bool overdueOnly = false,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var open = OpenStatuses();

        var query = Requests.AsNoTracking().AsQueryable();

        query = status is { } requestedStatus
            ? query.Where(request => request.Status == requestedStatus)
            // Unfiltered means "everything still needing attention", not literally everything
            // — a queue that includes decided items is not a queue.
            : query.Where(request => open.Contains(request.Status));

        if (type is { } requestedType)
        {
            query = query.Where(request => request.Type == requestedType);
        }

        if (assignedTo is not null)
        {
            query = query.Where(request => request.AssignedReviewerId == assignedTo);
        }

        if (overdueOnly && asOf is { } now)
        {
            query = query.Where(request => request.ReviewDueAt != null && request.ReviewDueAt < now);
        }

        var total = await query.CountAsync(cancellationToken);

        // Oldest deadline first, and items with no deadline last: an SLA that does not drive
        // the ordering is a number nobody acts on.
        var items = await query
            .OrderBy(request => request.ReviewDueAt == null)
            .ThenBy(request => request.ReviewDueAt)
            .ThenBy(request => request.SubmittedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Include(request => request.Documents)
            .ToListAsync(cancellationToken);

        return PagedResult<VerificationRequest>.Create(
            items, pagination.Page, pagination.PageSize, total);
    }

    public async Task<IReadOnlyList<VerificationRequest>> ListExpiredGrantsAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Requests
            .Where(request => (request.Status == VerificationStatus.Approved
                    || request.Status == VerificationStatus.PartiallyApproved)
                && request.ExpiresAt != null
                && request.ExpiresAt <= asOf)
            .OrderBy(request => request.ExpiresAt)
            .Take(limit)
            .Include(request => request.Documents)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        VerificationRequest request, CancellationToken cancellationToken = default) =>
        await Requests.AddAsync(request, cancellationToken);

    /// <summary>
    /// The statuses that still need someone to act. Materialised as an array rather than
    /// calling <c>VerificationWorkflow.IsOpen</c> because EF cannot translate a method call
    /// into SQL — it would silently pull the table into memory and filter there.
    /// </summary>
    private static VerificationStatus[] OpenStatuses() =>
    [
        VerificationStatus.Submitted,
        VerificationStatus.Pending,
        VerificationStatus.UnderReview,
        VerificationStatus.NeedMoreInfo,
        VerificationStatus.UnderInvestigation,
        VerificationStatus.Appealed
    ];
}

public sealed class TrustProfileRepository : ITrustProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public TrustProfileRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<TrustProfile> Profiles => _dbContext.Set<TrustProfile>();

    /// <summary>
    /// The member's profile, including one added earlier in this same unit of work.
    /// <para>
    /// <b>The change tracker is consulted first, and that is not an optimisation.</b> A
    /// LINQ query goes to the database and cannot see a pending insert, so a caller that
    /// provisions a profile and then asks for it back is told there is none — and provisions
    /// a second. <c>TrustProfileProvisioner</c> and <c>ReputationService</c> did exactly that
    /// in one request, which is a unique-constraint violation at save time on the reputation
    /// side and silently discarded country points on this one.
    /// </para>
    /// </summary>
    public async Task<TrustProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        Profiles.Local.FirstOrDefault(profile => profile.UserId == userId)
        ?? await Profiles
            .Include(profile => profile.Badges)
            .Include(profile => profile.Countries)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task<TrustProfile?> GetByUserReadOnlyAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        await Profiles
            .AsNoTracking()
            .Include(profile => profile.Badges)
            .Include(profile => profile.Countries)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task AddAsync(TrustProfile profile, CancellationToken cancellationToken = default) =>
        await Profiles.AddAsync(profile, cancellationToken);
}

public sealed class ReputationProfileRepository : IReputationProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ReputationProfileRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<ReputationProfile> Profiles => _dbContext.Set<ReputationProfile>();

    private DbSet<ReputationEvent> Events => _dbContext.Set<ReputationEvent>();

    /// <summary>
    /// Loads the profile <em>without</em> its events.
    /// <para>
    /// Deliberate. The events collection grows without bound, and every scoring call would
    /// otherwise load a three-year-old account's entire history to add two points. The
    /// aggregate appends to the collection, EF tracks the addition, and the existing rows are
    /// never needed — caps are answered by <see cref="GetRuleUsageAsync"/> instead.
    /// </para>
    /// </summary>
    /// <summary>
    /// The member's profile, including one added earlier in this same unit of work.
    /// <para>
    /// See the note on <see cref="TrustProfileRepository.GetByUserAsync"/>. Here the
    /// consequence was a 500: <c>GetOrCreateBothAsync</c> added a profile, this query did not
    /// see it, <c>ScoreAsync</c> added a second, and the save violated
    /// <c>ix_reputation_profiles_user</c>. It fired on every author's *first* confirmation,
    /// which is the one case a brand-new contributor is guaranteed to hit.
    /// </para>
    /// </summary>
    public async Task<ReputationProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        Profiles.Local.FirstOrDefault(profile => profile.UserId == userId)
        ?? await Profiles.FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task<ReputationProfile?> GetByUserReadOnlyAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        await Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task<PagedResult<ReputationEvent>> ListEventsAsync(
        UserId userId,
        PaginationRequest pagination,
        ReputationDimension? dimension = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var profileId = await Profiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return PagedResult<ReputationEvent>.Empty(pagination.Page, pagination.PageSize);
        }

        var query = Events.AsNoTracking().Where(item => item.ProfileId == profileId);

        if (dimension is { } requested)
        {
            query = query.Where(item => item.Dimension == requested);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ReputationEvent>.Create(
            items, pagination.Page, pagination.PageSize, total);
    }

    public async Task<RuleUsage> GetRuleUsageAsync(
        UserId userId,
        string actionType,
        string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var profileId = await Profiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return RuleUsage.None;
        }

        // Still one indexed aggregate. The per-country arm is a conditional sum in the same
        // pass rather than a second round trip: this runs on every scoring attempt, including
        // every comment written on the platform.
        //
        // A reversal (D-9) shares its original's action type and counts against it: a comment
        // that was removed gives its place under the cap back. The cooldown reads awards only, so
        // a reversal does not restart it.
        var usage = await Events
            .AsNoTracking()
            .Where(item => item.ProfileId == profileId && item.ActionType == actionType)
            .GroupBy(item => 1)
            .Select(group => new
            {
                Count = group.Count(item => item.ReversesEventId == null)
                    - group.Count(item => item.ReversesEventId != null),
                InCountry = countryCode == null
                    ? 0
                    : group.Count(item => item.CountryCode == countryCode && item.ReversesEventId == null)
                        - group.Count(item => item.CountryCode == countryCode && item.ReversesEventId != null),
                LastAt = group
                    .Where(item => item.ReversesEventId == null)
                    .Max(item => (DateTimeOffset?)item.CreatedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return usage is null
            ? RuleUsage.None
            : new RuleUsage(Math.Max(0, usage.Count), Math.Max(0, usage.InCountry), usage.LastAt);
    }

    public async Task<bool> HasAwardAsync(
        UserId userId, ReputationAwardKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        // The tracked side first, for the same reason GetByUserAsync looks there: a profile opened
        // and scored earlier in this unit of work is not in the database yet.
        var profileId = Profiles.Local.FirstOrDefault(profile => profile.UserId == userId)?.Id
            ?? await Profiles
                .Where(profile => profile.UserId == userId)
                .Select(profile => profile.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return false;
        }

        if (Events.Local.Any(item => item.ProfileId == profileId && item.AwardKey == key.Value))
        {
            return true;
        }

        return await Events
            .AsNoTracking()
            .AnyAsync(
                item => item.ProfileId == profileId && item.AwardKey == key.Value,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ReputationEventSummary>> SummariseEventsAsync(
        ReputationProfileId profileId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        // Grouped in the database, on ix_reputation_events_profile_action_created: a member with
        // three years of comments sends back a row per action, not a row per comment.
        var rows = await Events
            .AsNoTracking()
            .Where(item => item.ProfileId == profileId)
            .GroupBy(item => new { item.Dimension, item.ActionType })
            .Select(group => new
            {
                group.Key.Dimension,
                group.Key.ActionType,

                // Net of reversals (D-9): a guide published and then deleted is not a guide.
                Count = group.Count(item => item.ReversesEventId == null)
                    - group.Count(item => item.ReversesEventId != null),
                Points = group.Sum(item => item.Points)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ReputationEventSummary(row.Dimension, row.ActionType, row.Count, row.Points))
            .ToList();
    }

    public async Task AddAsync(
        ReputationProfile profile, CancellationToken cancellationToken = default) =>
        await Profiles.AddAsync(profile, cancellationToken);

    public async Task<ReputationProfile?> GetByIdAsync(
        ReputationProfileId profileId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        return Profiles.Local.FirstOrDefault(profile => profile.Id == profileId)
            ?? await Profiles.FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
    }

    public async Task<IReadOnlyList<ReputationEvent>> ListUnreversedBySourceAsync(
        string? sourceType, string sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var id = sourceId.Trim();

        // On ix_reputation_events_source. Reversals are excluded, and so is anything a reversal
        // already names, so a second delivery of a removal finds nothing left to undo.
        return await Events
            .AsNoTracking()
            .Where(item => item.SourceId == id
                && (sourceType == null || item.SourceType == sourceType)
                && item.ReversesEventId == null
                && !Events.Any(reversal => reversal.ReversesEventId == item.Id))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReputationEvent>> ListUnreversedByAwardKeysAsync(
        UserId userId, IReadOnlyCollection<string> awardKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(awardKeys);

        var profileId = await Profiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (profileId is null || awardKeys.Count == 0)
        {
            return [];
        }

        return await Events
            .AsNoTracking()
            .Where(item => item.ProfileId == profileId
                && item.AwardKey != null
                && awardKeys.Contains(item.AwardKey)
                && item.ReversesEventId == null
                && !Events.Any(reversal => reversal.ReversesEventId == item.Id))
            .ToListAsync(cancellationToken);
    }
}

public sealed class AppealRepository : IAppealRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AppealRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<Appeal> Appeals => _dbContext.Set<Appeal>();

    public async Task<Appeal?> GetAsync(AppealId id, CancellationToken cancellationToken = default) =>
        await Appeals
            .Include(appeal => appeal.Reviews)
            .FirstOrDefaultAsync(appeal => appeal.Id == id, cancellationToken);

    public async Task<Appeal?> GetOpenForTargetAsync(
        UserId userId, string targetId, CancellationToken cancellationToken = default)
    {
        var open = OpenStatuses();

        return await Appeals.FirstOrDefaultAsync(
            appeal => appeal.UserId == userId
                && appeal.TargetId == targetId
                && open.Contains(appeal.Status),
            cancellationToken);
    }

    public async Task<PagedResult<Appeal>> ListForUserAsync(
        UserId userId,
        PaginationRequest pagination,
        AppealStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var query = Appeals.AsNoTracking().Where(appeal => appeal.UserId == userId);

        if (status is { } requested)
        {
            query = query.Where(appeal => appeal.Status == requested);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(appeal => appeal.SubmittedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Appeal>.Create(items, pagination.Page, pagination.PageSize, total);
    }

    public async Task<PagedResult<Appeal>> ListQueueAsync(
        PaginationRequest pagination,
        AppealStatus? status = null,
        AppealType? type = null,
        UserId? assignedTo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var open = OpenStatuses();

        var query = Appeals.AsNoTracking().AsQueryable();

        query = status is { } requestedStatus
            ? query.Where(appeal => appeal.Status == requestedStatus)
            : query.Where(appeal => open.Contains(appeal.Status));

        if (type is { } requestedType)
        {
            query = query.Where(appeal => appeal.Type == requestedType);
        }

        if (assignedTo is not null)
        {
            query = query.Where(appeal => appeal.AssignedReviewerId == assignedTo);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(appeal => appeal.ReviewDueAt == null)
            .ThenBy(appeal => appeal.ReviewDueAt)
            .ThenBy(appeal => appeal.SubmittedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Appeal>.Create(items, pagination.Page, pagination.PageSize, total);
    }

    public async Task AddAsync(Appeal appeal, CancellationToken cancellationToken = default) =>
        await Appeals.AddAsync(appeal, cancellationToken);

    private static AppealStatus[] OpenStatuses() =>
        [AppealStatus.Pending, AppealStatus.UnderReview, AppealStatus.Escalated];
}

public sealed class TrustEventRepository : ITrustEventRepository
{
    private readonly DiasporaDbContext _dbContext;

    public TrustEventRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<TrustEvent> Events => _dbContext.Set<TrustEvent>();

    public async Task<PagedResult<TrustEvent>> ListForUserAsync(
        UserId userId,
        PaginationRequest pagination,
        bool memberVisibleOnly = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var query = Events.AsNoTracking().Where(item => item.UserId == userId);

        if (memberVisibleOnly)
        {
            query = query.Where(item => item.IsMemberVisible);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(item => item.OccurredAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<TrustEvent>.Create(items, pagination.Page, pagination.PageSize, total);
    }

    public async Task AddAsync(TrustEvent trustEvent, CancellationToken cancellationToken = default) =>
        await Events.AddAsync(trustEvent, cancellationToken);
}

public sealed class ModerationItemRepository : IModerationItemRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ModerationItemRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<ModerationItem> Items => _dbContext.Set<ModerationItem>();

    public async Task<ModerationItem?> GetAsync(
        ModerationItemId id, CancellationToken cancellationToken = default) =>
        await Items
            .Include(item => item.Decisions)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<ModerationItem?> GetOpenForContentAsync(
        ModerationContentType contentType, string contentId, CancellationToken cancellationToken = default)
    {
        var open = OpenStatuses();

        return await Items.FirstOrDefaultAsync(
            item => item.ContentType == contentType
                && item.ContentId == contentId
                && open.Contains(item.Status),
            cancellationToken);
    }

    public async Task<PagedResult<ModerationItem>> ListQueueAsync(
        PaginationRequest pagination,
        ModerationQueueStatus? status = null,
        ModerationContentType? contentType = null,
        ModerationPriority? minimumPriority = null,
        UserId? assignedTo = null,
        bool overdueOnly = false,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var open = OpenStatuses();

        var query = Items.AsNoTracking().AsQueryable();

        query = status is { } requestedStatus
            ? query.Where(item => item.Status == requestedStatus)
            : query.Where(item => open.Contains(item.Status));

        if (contentType is { } requestedType)
        {
            query = query.Where(item => item.ContentType == requestedType);
        }

        if (minimumPriority is { } priority)
        {
            // Stored by name: as text, "Normal or above" left out High, and "High or above" let
            // Low and Normal in. See StoredEnum.
            var admitted = StoredEnum.AtLeast(priority);
            query = query.Where(item => admitted.Contains(item.Priority));
        }

        if (assignedTo is not null)
        {
            query = query.Where(item => item.AssignedModeratorId == assignedTo);
        }

        if (overdueOnly && asOf is { } now)
        {
            query = query.Where(item => item.DueAt != null && item.DueAt < now);
        }

        var total = await query.CountAsync(cancellationToken);

        // Priority first, then age. An urgent item from a minute ago outranks a low-priority
        // one from yesterday — otherwise the priority column is decoration. By rank: sorted by its
        // stored name the queue read Urgent, Normal, Low, High, with High-priority work last.
        var items = await query
            .OrderByDescending(StoredEnum.Rank((ModerationItem item) => item.Priority))
            .ThenBy(item => item.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ModerationItem>.Create(items, pagination.Page, pagination.PageSize, total);
    }

    public async Task AddAsync(ModerationItem item, CancellationToken cancellationToken = default) =>
        await Items.AddAsync(item, cancellationToken);

    private static ModerationQueueStatus[] OpenStatuses() =>
    [
        ModerationQueueStatus.AIReview,
        ModerationQueueStatus.PendingReview,
        ModerationQueueStatus.Escalated,
        ModerationQueueStatus.Appealed
    ];
}

public sealed class CommunityContributionRepository : ICommunityContributionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityContributionRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<CommunityContribution> Contributions => _dbContext.Set<CommunityContribution>();

    public async Task<bool> ExistsAsync(
        UserId confirmedBy,
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        var normalized = targetType?.Trim().ToLowerInvariant();

        return await Contributions.AnyAsync(
            contribution => contribution.ConfirmedByUserId == confirmedBy
                && contribution.TargetType == normalized
                && contribution.TargetId == targetId,
            cancellationToken);
    }

    public async Task<int> CountForTargetAsync(
        string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        var normalized = targetType?.Trim().ToLowerInvariant();

        return await Contributions.CountAsync(
            contribution => contribution.TargetType == normalized
                && contribution.TargetId == targetId,
            cancellationToken);
    }

    public async Task AddAsync(
        CommunityContribution contribution, CancellationToken cancellationToken = default) =>
        await Contributions.AddAsync(contribution, cancellationToken);
}
