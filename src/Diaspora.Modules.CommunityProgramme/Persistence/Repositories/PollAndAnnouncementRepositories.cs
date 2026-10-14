using Diaspora.Modules.CommunityProgramme.Domain.Announcements;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>
/// Reads and writes polls and their responses.
/// <para>
/// <b>The tally is a <c>GROUP BY</c> in the database and must stay one.</b> Loading every response to
/// count them is the shape that works on a poll with four answers and falls over on the one that
/// matters — and a poll put to a country has as many responses as there are members.
/// </para>
/// </summary>
public sealed class CommunityPollRepository : ICommunityPollRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityPollRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityPoll> Polls => _dbContext.Set<CommunityPoll>();

    private DbSet<PollResponse> Responses => _dbContext.Set<PollResponse>();

    public async Task<CommunityPoll?> GetAsync(
        CommunityPollId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Polls.FirstOrDefaultAsync(poll => poll.Id == id, cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityPoll>> SearchAsync(
        PollQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Polls.AsNoTracking();

        var managed = filter.ManagedOrganizationIds?.ToList() ?? [];
        var memberOf = filter.MemberOfOrganizationIds?.ToList() ?? [];
        var viewer = filter.ViewerUserId;

        // Two rules in one predicate, and both are the server's rather than the client's.
        //
        //   A draft is visible only to somebody who manages the body that owns it.
        //
        //   A members-only poll appears only for a member of that body, an invitation-only poll only
        //   for somebody named on it. The register asks for a 403 on the detail read; the list has to
        //   agree, and it agrees by never selecting the row — a private question that reached the
        //   device has already been disclosed whether or not the client drew it.
        query = query.Where(poll =>
            (poll.Status != PollStatus.Draft || managed.Contains(poll.OrganizationId!.Value))
            && (poll.Audience == PollAudience.Public
                || poll.Audience == PollAudience.Country
                || poll.Audience == PollAudience.Neighborhood
                || (poll.Audience == PollAudience.OrganizationMembers
                    && poll.OrganizationId != null
                    && (memberOf.Contains(poll.OrganizationId.Value)
                        || managed.Contains(poll.OrganizationId.Value)))
                || (poll.Audience == PollAudience.Invited
                    && viewer != null
                    && poll.Invitees.Any(invitee => invitee.UserId == viewer))));

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(poll => poll.OrganizationId == organizationId);
        }

        if (filter.Audience is { } audience)
        {
            query = query.Where(poll => poll.Audience == audience);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(poll => statuses.Contains(poll.Status));
        }

        if (filter.NeighborhoodAreaId is { } areaId)
        {
            query = query.Where(poll => poll.NeighborhoodAreaId == areaId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            // Lowered on both sides: Postgres compares text case-sensitively and SQLite does not.
            var city = filter.CityName.Trim().ToLowerInvariant();

#pragma warning disable CA1304, CA1311, CA1862 // Inside an expression tree: EF translates ToLower to SQL lower().
            query = query.Where(poll => poll.CityName != null && poll.CityName.ToLower() == city);
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();

            query = query.Where(poll =>
                poll.CountryCode == null || poll.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(poll => EF.Functions.Like(poll.Question, $"%{term}%"));
        }

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        query = CursorPage.Apply(
            query, cursor, CommunityPollId.From, poll => poll.CreatedAt, poll => poll.Id);

        query = query
            .OrderByDescending(poll => poll.CreatedAt)
            .ThenBy(poll => poll.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            poll => poll.CreatedAt,
            poll => poll.Id.Value,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityPoll>> FindDueToOpenAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Polls
            .Where(poll => poll.Status == PollStatus.Scheduled
                && poll.OpensAt != null
                && poll.OpensAt <= asOf)
            .OrderBy(poll => poll.OpensAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CommunityPoll>> FindDueToCloseAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Polls
            .Where(poll => poll.Status == PollStatus.Open
                && poll.ClosesAt != null
                && poll.ClosesAt <= asOf)
            .OrderBy(poll => poll.ClosesAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

    public Task<int> CountOpenAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        Polls
            .AsNoTracking()
            .CountAsync(
                poll => poll.OrganizationId == organizationId && poll.Status == PollStatus.Open,
                cancellationToken);

    public void Add(CommunityPoll poll)
    {
        ArgumentNullException.ThrowIfNull(poll);

        Polls.Add(poll);
    }

    // ── Responses ───────────────────────────────────────────────────────────────────────────

    public async Task<PollResponse?> FindResponseAsync(
        CommunityPollId pollId, string voterFingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollId);

        if (string.IsNullOrWhiteSpace(voterFingerprint))
        {
            return null;
        }

        return await Responses.FirstOrDefaultAsync(
            response => response.PollId == pollId
                && response.VoterFingerprint == voterFingerprint,
            cancellationToken);
    }

    public async Task<PollResponse?> FindResponseByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Responses.FirstOrDefaultAsync(
            response => response.IdempotencyKey == normalized, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<PollOptionId, int>> TallyAsync(
        CommunityPollId pollId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollId);

        // SelectMany over the owned selections, grouped in the database. The count is one row per
        // option regardless of how many people voted — which is the property that makes this read
        // survive a poll with a hundred thousand responses.
        var rows = await Responses
            .AsNoTracking()
            .Where(response => response.PollId == pollId)
            .SelectMany(response => response.Selections)
            .GroupBy(selection => selection.OptionId)
            .Select(group => new { OptionId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.OptionId, row => row.Count);
    }

    public Task<int> CountResponsesAsync(
        CommunityPollId pollId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollId);

        return Responses
            .AsNoTracking()
            .CountAsync(response => response.PollId == pollId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListOpenResponsesAsync(
        CommunityPollId pollId, int limit = 200, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollId);

        return await Responses
            .AsNoTracking()
            .Where(response => response.PollId == pollId && response.OpenResponse != null)
            .OrderByDescending(response => response.RespondedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(response => response.OpenResponse!)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> FindResponseMomentsAsync(
        Guid organizationId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        // The poll identifiers first, then the responses against them. Two round trips rather than a
        // join, because the response table does not carry an organization and denormalising one onto
        // a vote would put an identifier on a row that is meant to carry as little as possible.
        var pollIds = await Polls
            .AsNoTracking()
            .Where(poll => poll.OrganizationId == organizationId)
            .Select(poll => poll.Id)
            .ToListAsync(cancellationToken);

        if (pollIds.Count == 0)
        {
            return [];
        }

        return await Responses
            .AsNoTracking()
            .Where(response => pollIds.Contains(response.PollId)
                && response.RespondedAt >= from
                && response.RespondedAt <= to)
            .Select(response => response.RespondedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountResponsesForVoterAsync(
        UserId voterUserId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voterUserId);

        var query = Responses
            .AsNoTracking()
            .Where(response => response.VoterUserId == voterUserId);

        if (from is { } start)
        {
            query = query.Where(response => response.RespondedAt >= start);
        }

        if (to is { } end)
        {
            query = query.Where(response => response.RespondedAt <= end);
        }

        return query.CountAsync(cancellationToken);
    }

    public void AddResponse(PollResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        Responses.Add(response);
    }
}

/// <summary>Reads and writes announcements.</summary>
public sealed class CommunityAnnouncementRepository : ICommunityAnnouncementRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityAnnouncementRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityAnnouncement> Announcements =>
        _dbContext.Set<CommunityAnnouncement>();

    public async Task<CommunityAnnouncement?> GetAsync(
        CommunityAnnouncementId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Announcements.FirstOrDefaultAsync(
            announcement => announcement.Id == id, cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityAnnouncement>> SearchAsync(
        AnnouncementQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Announcements.AsNoTracking();

        var memberOf = filter.MemberOfOrganizationIds?.ToList() ?? [];

        // The audience-resolution rule the register asks for, applied in the query: an organization
        // announcement reaches its members and nobody else. Everything else is addressed to a place
        // or a country and is public within it.
        query = query.Where(announcement =>
            announcement.Scope != AnnouncementScope.Organization
            || (announcement.OrganizationId != null
                && memberOf.Contains(announcement.OrganizationId.Value)));

        if (filter.Scope is { } scope)
        {
            query = query.Where(announcement => announcement.Scope == scope);
        }

        if (filter.ScopeId is { } scopeId)
        {
            query = query.Where(announcement => announcement.ScopeId == scopeId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();

            query = query.Where(announcement =>
                announcement.CountryCode == null || announcement.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(announcement =>
                EF.Functions.Like(announcement.Title, $"%{term}%")
                || EF.Functions.Like(announcement.Body, $"%{term}%"));
        }

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        query = CursorPage.Apply(
            query,
            cursor,
            CommunityAnnouncementId.From,
            announcement => announcement.PublishedAt,
            announcement => announcement.Id);

        // Pinned first, then most recent. The cursor still sorts on PublishedAt, so a pinned
        // announcement published long ago sits at the top of page one and does not reappear later:
        // the boundary follows the published moment, and every pinned row is served before any
        // unpinned one regardless of where its timestamp falls.
        query = query
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenByDescending(announcement => announcement.PublishedAt)
            .ThenBy(announcement => announcement.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            announcement => announcement.PublishedAt,
            announcement => announcement.Id.Value,
            cancellationToken);
    }

    public async Task<CommunityAnnouncement?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Announcements.FirstOrDefaultAsync(
            announcement => announcement.IdempotencyKey == normalized, cancellationToken);
    }

    public Task<int> CountSinceAsync(
        Guid organizationId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Announcements
            .AsNoTracking()
            .CountAsync(
                announcement => announcement.OrganizationId == organizationId
                    && announcement.PublishedAt >= since,
                cancellationToken);

    public void Add(CommunityAnnouncement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        Announcements.Add(announcement);
    }
}
