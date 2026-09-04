using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// FRONTEND-PHASE13-GAP-001 — the community directory.
/// <para>
/// <b>The gap this closes was not "the list is wrong"; it was that there was no list.</b>
/// <c>GET /communities</c> answered 404, and the client had been built for a whole phase against a
/// simulated adapter whose communities were derived from a country list — which is the one thing
/// §82.2 exists to eliminate, because two communities can share a country, a regional community has
/// none, and an interest community has no geography at all.
/// </para>
/// <para>
/// <b>Anonymous, deliberately.</b> The register is explicit that an unauthenticated caller reads
/// public communities and receives <c>membershipState: "None"</c> rather than an error. Requiring a
/// token to browse would make the directory unreachable from the one screen that most needs it —
/// the one somebody sees before they have an account.
/// </para>
/// </summary>
public sealed record GetCommunityDirectoryQuery
    : IQuery<CommunityPageResponse<CommunityDirectoryEntryResponse>>
{
    /// <summary>Matches name and tagline. Absent means everything.</summary>
    public string? Query { get; init; }

    /// <summary>
    /// Comma-separated <c>CommunityType</c> names, as the client sends them.
    /// <para>
    /// <b>A string and not a <c>string[]</c>, and this is the whole reason the type is spelled out
    /// here.</b> The client sends <c>?types=NationalDiaspora,CityChapter</c> — one parameter, one
    /// comma-joined value. A <c>string[]</c> parameter binds repeated keys, so it would take that as
    /// a single element <c>"NationalDiaspora,CityChapter"</c>, match no type, and quietly serve an
    /// unfiltered browse under a lit filter chip. That defect has already happened on this platform
    /// four times in one file.
    /// </para>
    /// </summary>
    public string? Types { get; init; }

    /// <summary>Comma-separated ISO country codes. Where communities are <em>from</em>.</summary>
    public string? OriginCountryCodes { get; init; }

    /// <summary>Where communities have a <em>presence</em>, which is a different question.</summary>
    public string? PresenceCountryCode { get; init; }

    /// <summary>Whether to include communities the caller already belongs to. Defaults true.</summary>
    public bool IncludeJoined { get; init; } = true;

    /// <summary>See <see cref="GetCommunityDirectoryQueryHandler"/> on what is served.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed class GetCommunityDirectoryQueryHandler
    : IQueryHandler<GetCommunityDirectoryQuery, CommunityPageResponse<CommunityDirectoryEntryResponse>>
{
    /// <summary>
    /// The states that mean "already in", for <c>includeJoined=false</c>.
    /// <para>
    /// Following and Interested are not in it. Somebody following a community has not joined it, and
    /// hiding it from a browse would hide the very community they are closest to joining.
    /// </para>
    /// </summary>
    private static readonly CommunityMembershipState[] Joined =
    [
        CommunityMembershipState.Member,
        CommunityMembershipState.VerifiedMember,
        CommunityMembershipState.PendingApproval
    ];

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;

    public GetCommunityDirectoryQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
    }

    public async Task<Result<CommunityPageResponse<CommunityDirectoryEntryResponse>>> Handle(
        GetCommunityDirectoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sort = ReadSort(query.Sort);

        if (sort.IsFailure)
        {
            return Result.Failure<CommunityPageResponse<CommunityDirectoryEntryResponse>>(sort.Error);
        }

        var viewer = _access.CallerId;

        // Communities to leave out, resolved before the query rather than filtered from the page
        // afterwards: dropping rows after paging returns short pages, and a short page reads to an
        // infinite-scroll list as the end of the directory.
        var excluded = !query.IncludeJoined && viewer is not null
            ? await JoinedByAsync(viewer, cancellationToken)
            : [];

        var page = await _communities.SearchAsync(
            new CommunityDirectoryFilter
            {
                Query = query.Query,
                Types = ReadTypes(query.Types),
                OriginCountryCodes = Split(query.OriginCountryCodes),
                PresenceCountryCode = query.PresenceCountryCode,
                ExcludeCommunityIds = excluded,
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Limit)
            },
            cancellationToken);

        // One query for the caller's standing across the whole page. Asking per row is the N+1 that
        // makes a directory unusable at the scale §77 requires it to survive.
        var standing = viewer is null || page.Items.Count == 0
            ? new Dictionary<CommunityId, CommunityMembership>()
            : await _memberships.FindForMemberAsync(
                viewer, [.. page.Items.Select(community => community.Id)], cancellationToken);

        var entries = page.Items
            .Select(community => CommunityMapper.ToEntry(
                community,
                standing.GetValueOrDefault(community.Id)))
            .ToList();

        return Result.Success(new CommunityPageResponse<CommunityDirectoryEntryResponse>
        {
            Items = entries,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,

            // Absent rather than guessed. Counting the directory is a second scan of the same
            // predicate to answer a question the extra row already answered.
            TotalCount = null
        });
    }

    /// <summary>
    /// Which orderings this route serves, and which it refuses.
    /// <para>
    /// <b>Newest-first is the only order, and <c>relevance</c> and <c>recent</c> both mean it.</b>
    /// With no search term they are the same list; with one, the filter is a substring match rather
    /// than a ranked search, so calling the result "relevance" would be the more honest of two
    /// imprecise words rather than a different order.
    /// </para>
    /// <para>
    /// <b><c>members</c> is refused because §31 forbids implying a popularity the backend does not
    /// authoritatively report.</b> The count is maintained by the aggregate as members come and go,
    /// not measured, and an order built on it would present a number as a ranking.
    /// </para>
    /// <para>
    /// <b><c>name</c> is refused because this module's cursor is keyed on a timestamp.</b> Ordering
    /// by name needs a name-keyed boundary — a second scheme, with its own tie-break and its own
    /// escaping, since a community name may contain the token's separator. Accepting the parameter
    /// and paging in timestamp order regardless is precisely the silent-fallback defect the register
    /// was written about, and a refusal the client can render is the better half of the trade until
    /// that scheme exists.
    /// </para>
    /// </summary>
    private static Result<string> ReadSort(string? sort)
    {
        var requested = sort?.Trim();

        if (string.IsNullOrEmpty(requested))
        {
            return Result.Success("recent");
        }

        return requested.ToLowerInvariant() switch
        {
            "relevance" or "recent" => Result.Success("recent"),
            "members" => Result.Failure<string>(CommunityErrors.SortByMembersUnavailable),
            "name" => Result.Failure<string>(CommunityErrors.SortByNameUnavailable),
            _ => Result.Failure<string>(CommunityErrors.SortUnknown)
        };
    }

    private async Task<IReadOnlyList<CommunityId>> JoinedByAsync(
        UserId viewer, CancellationToken cancellationToken)
    {
        var rows = await _memberships.FindForMemberAsync(viewer, null, cancellationToken);

        return [.. rows
            .Where(entry => Joined.Contains(entry.Value.State))
            .Select(entry => entry.Key)];
    }

    /// <summary>
    /// Reads the comma-separated type list.
    /// <para>
    /// An unrecognised name is dropped rather than refused — a newer client naming a type this
    /// deployment does not have should get the types it does, not a 400 on the whole browse. A list
    /// that names <em>only</em> unknown types is treated as no filter for the same reason.
    /// </para>
    /// </summary>
    private static IReadOnlyList<CommunityType>? ReadTypes(string? types)
    {
        var names = Split(types);

        if (names is not { Count: > 0 })
        {
            return null;
        }

        var parsed = names
            .Select(name => Enum.TryParse<CommunityType>(name, ignoreCase: true, out var type)
                ? type
                : (CommunityType?)null)
            .OfType<CommunityType>()
            .Distinct()
            .ToList();

        return parsed.Count > 0 ? parsed : null;
    }

    private static IReadOnlyList<string>? Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : [.. value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
