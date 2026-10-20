using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>
/// Reads and writes communities.
/// <para>
/// <b>Every read here is visibility-blind, and that is deliberate.</b> This layer answers "what is
/// stored"; whether the caller may see it is the handler's decision, made once, where the caller is
/// known. A repository that filtered on visibility would leave every caller unsure whether an empty
/// result meant absence or refusal — and the directory search is the one exception, because there
/// the filter is the query rather than an authorization check.
/// </para>
/// </summary>
public sealed class CommunityRepository : ICommunityRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityAggregate> Communities => _dbContext.Set<CommunityAggregate>();

    public async Task<CommunityAggregate?> GetAsync(
        CommunityId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Communities.FirstOrDefaultAsync(
            community => community.Id == id, cancellationToken);
    }

    public async Task<CommunityAggregate?> FindBySlugAsync(
        CommunitySlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        return await Communities.FirstOrDefaultAsync(
            community => community.Slug == slug, cancellationToken);
    }

    public async Task<CommunityAggregate?> FindByIdOrSlugAsync(
        string idOrSlug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return null;
        }

        var candidate = idOrSlug.Trim();

        // A Guid is read as an identifier; anything else is read as a handle. A handle can never be
        // mistaken for a Guid — the slug scheme has no braces or dashes in that arrangement — so
        // the two spaces cannot collide.
        if (Guid.TryParse(candidate, out var parsed))
        {
            return await GetAsync(CommunityId.From(parsed), cancellationToken);
        }

        var slug = CommunitySlug.Create(candidate);

        // A malformed handle is not an error here: it is a lookup for something that cannot exist,
        // and the handler turns that into the same "no such community" everything else gets.
        return slug.IsFailure
            ? null
            : await FindBySlugAsync(slug.Value, cancellationToken);
    }

    public async Task<bool> IsSlugTakenAsync(
        CommunitySlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        return await Communities
            .AsNoTracking()
            .AnyAsync(community => community.Slug == slug, cancellationToken);
    }

    /// <summary>
    /// The directory.
    /// <para>
    /// <b>Only public, live communities are ever returned.</b> Unlisted is readable by anyone
    /// holding its link and never listed; private answers exactly as absent. That is enforced here
    /// rather than by a caller, because the directory is the one place where forgetting it leaks the
    /// existence of every private community on the platform.
    /// </para>
    /// <para>
    /// <b>Pending communities are excluded too.</b> A community that appeared in the directory the
    /// moment somebody typed a name would make the directory a scratchpad; suspended ones stay, so
    /// members can still find a community they belong to and read why it stopped.
    /// </para>
    /// </summary>
    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification =
            "The ToLower calls below are inside an expression tree. They are never executed in "
            + ".NET: EF translates them to SQL lower(), which runs under the database collation. "
            + "There is no CultureInfo to specify, and passing one would fail to translate.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304. ToLowerInvariant translates identically; neither is executed.")]
    [SuppressMessage(
        "Globalization", "CA1862:Use the StringComparison method overloads",
        Justification =
            "Contains(string, StringComparison) has no translation on either provider, so the "
            + "suggested overload would move the whole directory search into memory. Lowering both "
            + "sides is what makes the match case-insensitive on Postgres, where LIKE is not.")]
    public async Task<CursorPagedResult<CommunityAggregate>> SearchAsync(
        CommunityDirectoryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Communities
            .AsNoTracking()
            .Where(community =>
                community.Visibility == CommunityVisibility.Public
                && (community.Status == CommunityStatus.Active
                    || community.Status == CommunityStatus.Suspended));

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            // Matched case-insensitively against the two fields a person reads on a card.
            //
            // `EF.Functions.ILike` was the obvious choice and does not work: it is Npgsql-only and
            // throws "could not be translated" on SQLite, which the integration suite runs. Lowering
            // both sides translates on every provider.
            //
            // `EF.Functions.Like` is what three other repositories in this module use, and it is
            // not right here: LIKE is case-sensitive on Postgres, so a member typing "nigerians in
            // london" would not find "Nigerians in London" — on the one screen the whole phase is
            // built around. Lowering both sides is what makes it match.
            //
            // It also means no index is used for the text match. That is the right trade at this
            // size — the filters that narrow hardest, origin and type, are indexed — and the fix
            // when it stops being right is a full-text index, not a different expression here.
            var term = filter.Query.Trim().ToLowerInvariant();

            query = query.Where(community =>
                community.Name.ToLower().Contains(term)
                || (community.Tagline != null && community.Tagline.ToLower().Contains(term)));
        }

        if (filter.Types is { Count: > 0 })
        {
            var types = filter.Types.ToList();
            query = query.Where(community => types.Contains(community.Type));
        }

        if (filter.OriginCountryCodes is { Count: > 0 })
        {
            var origins = filter.OriginCountryCodes
                .Select(code => code?.Trim().ToUpperInvariant())
                .Where(code => !string.IsNullOrEmpty(code))
                .ToList();

            if (origins.Count > 0)
            {
                query = query.Where(community =>
                    community.OriginCountryCode != null
                    && origins.Contains(community.OriginCountryCode));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.PresenceCountryCode))
        {
            var presence = filter.PresenceCountryCode.Trim().ToUpperInvariant();
            query = query.Where(community => community.PresenceCountryCodes.Contains(presence));
        }

        if (filter.ExcludeCommunityIds is { Count: > 0 })
        {
            var excluded = filter.ExcludeCommunityIds.ToList();
            query = query.Where(community => !excluded.Contains(community.Id));
        }

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        query = CursorPage.Apply(
            query,
            cursor,
            CommunityId.From,
            community => community.CreatedAt,
            community => community.Id);

        query = query
            .OrderByDescending(community => community.CreatedAt)
            .ThenBy(community => community.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            community => community.CreatedAt,
            community => community.Id.Value,
            cancellationToken);
    }

    public void Add(CommunityAggregate community)
    {
        ArgumentNullException.ThrowIfNull(community);
        Communities.Add(community);
    }
}

/// <summary>
/// Reads and writes standing.
/// <para>
/// <b>No read here filters by state.</b> Every caller has to say which states it counts, because
/// the presence of a row means nothing on its own — somebody who left, was declined or was revoked
/// all keep one. A repository that quietly returned only current members would make a revoked
/// member look like a stranger, and rejoining would silently succeed.
/// </para>
/// </summary>
public sealed class CommunityMembershipRepository : ICommunityMembershipRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityMembershipRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityMembership> Memberships => _dbContext.Set<CommunityMembership>();

    public async Task<CommunityMembership?> GetAsync(
        CommunityMembershipId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.Id == id, cancellationToken);
    }

    public async Task<CommunityMembership?> FindAsync(
        CommunityId communityId, UserId memberId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);
        ArgumentNullException.ThrowIfNull(memberId);

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.CommunityId == communityId && membership.MemberId == memberId,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<CommunityId, CommunityMembership>> FindForMemberAsync(
        UserId memberId,
        IReadOnlyCollection<CommunityId>? communityIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        var query = Memberships
            .AsNoTracking()
            .Where(membership => membership.MemberId == memberId);

        if (communityIds is { Count: > 0 })
        {
            var ids = communityIds.ToList();
            query = query.Where(membership => ids.Contains(membership.CommunityId));
        }

        var rows = await query.ToListAsync(cancellationToken);

        // The unique index guarantees one row per member per community, so this cannot collide.
        return rows.ToDictionary(membership => membership.CommunityId);
    }

    public async Task<CommunityMembership?> FindPrimaryAsync(
        UserId memberId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.MemberId == memberId && membership.IsPrimary,
            cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityMembership>> ListVisibleMembersAsync(
        CommunityId communityId,
        UserId viewer,
        string? cursor,
        int limit,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        // States spelled out: `IsCurrentMember` is a computed property and does not translate.
        var query = Memberships
            .AsNoTracking()
            .Where(membership =>
                membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember)
                && (membership.Visibility != CommunityMembershipVisibility.Private
                    || membership.MemberId == viewer));

        // 21 Oct 2026, the role-holder filter. Member is implied by belonging and never stored, so
        // the caller passes null for it and every member is one.
        if (role is not null)
        {
            query = query.Where(membership => membership.Roles.Contains(role));
        }

        var page = CommunityProgrammeCursor.Parse(cursor);

        query = CursorPage.Apply(
            query,
            page,
            CommunityMembershipId.From,
            membership => membership.CreatedAt,
            membership => membership.Id);

        query = query
            .OrderByDescending(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            page,
            Math.Clamp(limit, 1, CommunityProgrammeLimits.MaxPageSize),
            membership => membership.CreatedAt,
            membership => membership.Id.Value,
            cancellationToken);
    }

    public Task<CommunityMembership?> FindOwnerAsync(
        CommunityId communityId, CancellationToken cancellationToken = default) =>
        Memberships.FirstOrDefaultAsync(
            membership =>
                membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember)
                && membership.Roles.Contains("Owner"),
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListCurrentMemberIdsAsync(
        CommunityId communityId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var members = await Memberships
            .AsNoTracking()
            .Where(membership =>
                membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember))
            // A stable order, or a page boundary moves under a large broadcast and somebody is
            // notified twice while somebody else is skipped.
            .OrderBy(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(0, take))
            .Select(membership => membership.MemberId)
            .ToListAsync(cancellationToken);

        return [.. members.Select(member => member.Value)];
    }

    public void Add(CommunityMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        Memberships.Add(membership);
    }
}

/// <summary>
/// Reads and writes requests to close a community — 11 Sep 2026.
/// <para>
/// Nothing here filters on who may see a request: this layer answers "what is stored", and whether
/// the caller is the owner or a Diaspora administrator is the handler's decision, made once.
/// </para>
/// </summary>
public sealed class CommunityDeletionRequestRepository : ICommunityDeletionRequestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityDeletionRequestRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityDeletionRequest> Requests => _dbContext.Set<CommunityDeletionRequest>();

    public async Task<CommunityDeletionRequest?> GetAsync(
        CommunityDeletionRequestId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Requests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<CommunityDeletionRequest?> FindOpenAsync(
        CommunityId communityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);

        return await Requests
            .Where(request =>
                request.CommunityId == communityId
                && (request.Status == CommunityDeletionStatus.AwaitingReview
                    || request.Status == CommunityDeletionStatus.Approved))
            .OrderByDescending(request => request.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CommunityDeletionRequest?> FindLatestAsync(
        CommunityId communityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);

        return await Requests
            .Where(request => request.CommunityId == communityId)
            .OrderByDescending(request => request.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityDeletionRequest>> ListAsync(
        CommunityDeletionStatus? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Requests.AsNoTracking();

        if (status is { } wanted)
        {
            query = query.Where(request => request.Status == wanted);
        }

        var parsed = CommunityProgrammeCursor.Parse(cursor);

        query = CursorPage.Apply(
            query,
            parsed,
            CommunityDeletionRequestId.From,
            request => request.RequestedAt,
            request => request.Id);

        query = query
            .OrderByDescending(request => request.RequestedAt)
            .ThenBy(request => request.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            parsed,
            Math.Clamp(limit, 1, CommunityProgrammeLimits.MaxPageSize),
            request => request.RequestedAt,
            request => request.Id.Value,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDeletionRequest>> ListDueAsync(
        DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
    {
        var due = await Requests
            .Where(request =>
                request.Status == CommunityDeletionStatus.Approved
                && request.EffectiveAt <= now)
            .OrderBy(request => request.EffectiveAt)
            .Take(Math.Clamp(batchSize, 1, CommunityProgrammeLimits.MaxPageSize))
            .ToListAsync(cancellationToken);

        return due;
    }

    public void Add(CommunityDeletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Add(request);
    }
}

/// <summary>Reads and writes offers to hand a community over — 11 Sep 2026.</summary>
public sealed class CommunityOwnershipTransferRepository : ICommunityOwnershipTransferRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityOwnershipTransferRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<CommunityOwnershipTransfer> Transfers =>
        _dbContext.Set<CommunityOwnershipTransfer>();

    public async Task<CommunityOwnershipTransfer?> GetAsync(
        CommunityOwnershipTransferId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Transfers.FirstOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);
    }

    public async Task<CommunityOwnershipTransfer?> FindOpenAsync(
        CommunityId communityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);

        return await Transfers
            .Where(transfer =>
                transfer.CommunityId == communityId
                && transfer.Status == CommunityOwnershipTransferStatus.Offered)
            .OrderByDescending(transfer => transfer.OfferedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityOwnershipTransfer>> ListOpenForMemberAsync(
        UserId memberId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        return await Transfers
            .Where(transfer =>
                transfer.OfferedTo == memberId
                && transfer.Status == CommunityOwnershipTransferStatus.Offered)
            .OrderByDescending(transfer => transfer.OfferedAt)
            .Take(CommunityProgrammeLimits.DefaultPageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityOwnershipTransfer>> ListLapsedAsync(
        DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
    {
        return await Transfers
            .Where(transfer =>
                transfer.Status == CommunityOwnershipTransferStatus.Offered
                && transfer.ExpiresAt <= now)
            .OrderBy(transfer => transfer.ExpiresAt)
            .Take(Math.Clamp(batchSize, 1, CommunityProgrammeLimits.MaxPageSize))
            .ToListAsync(cancellationToken);
    }

    public void Add(CommunityOwnershipTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        Transfers.Add(transfer);
    }
}
