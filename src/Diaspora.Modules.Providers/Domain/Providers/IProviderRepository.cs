using Diaspora.Contracts.Providers.V1;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// How the module loads and stores a provider it is about to change.
/// <para>
/// Identity-based access, the slug uniqueness check that guards registration, and the lapsed
/// verification batch the sweeper loads in order to act on it.
/// </para>
/// </summary>
public interface IProviderRepository
{
    /// <summary>The full aggregate. For commands.</summary>
    Task<Provider?> GetAsync(ProviderId id, CancellationToken cancellationToken = default);

    /// <summary>The profile-screen graph. Everything the client's provider detail renders.</summary>
    Task<Provider?> GetForReadAsync(ProviderId id, CancellationToken cancellationToken = default);

    /// <summary>The profile-screen graph, by the deep-link handle.</summary>
    Task<Provider?> GetBySlugAsync(ProviderSlug slug, CancellationToken cancellationToken = default);

    /// <summary>Several providers' card graphs at once. The N+1 guard for a bookmark list.</summary>
    Task<IReadOnlyList<Provider>> GetManyAsync(
        IReadOnlyCollection<ProviderId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a slug is free.
    /// <para>
    /// Advisory, not authoritative: the unique index is what actually guarantees it. This exists
    /// so registration can pick a free suffix before writing, rather than catching a constraint
    /// violation and retrying — which works but produces a gap in the identifier sequence and an
    /// error in every log.
    /// </para>
    /// </summary>
    Task<bool> SlugExistsAsync(ProviderSlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Providers holding a verification that has lapsed but is still marked current.
    /// The sweeper's query — a badge must come off when the grant expires, not when the
    /// provider next happens to be edited.
    /// </summary>
    Task<IReadOnlyList<Provider>> ListWithLapsedVerificationsAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    Task AddAsync(Provider provider, CancellationToken cancellationToken = default);

    void Remove(Provider provider);
}

/// <summary>
/// The directory path: a page of providers, how many match, and the nearest few.
/// <para>
/// Changes when the directory screen changes rather than when the aggregate does.
/// <see cref="GetManyAsync"/> is declared here as well, because hydrating the ids a search
/// returned is the same operation as loading a batch by id.
/// </para>
/// </summary>
public interface IProviderSearch
{
    /// <summary>The one query discovery, nearby, similar and "mine" all run.</summary>
    Task<CursorPagedResult<Provider>> QueryAsync(
        ProviderQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>How many providers match, when a caller genuinely needs a total.</summary>
    Task<int> CountAsync(ProviderQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every match as identifiers and positions only, up to <paramref name="limit"/>. For a
    /// "nearest first" search, which orders by a distance only Location may measure.
    /// </summary>
    Task<IReadOnlyList<ProviderNearestCandidate>> QueryNearestCandidatesAsync(
        ProviderQueryFilter filter, int limit, CancellationToken cancellationToken = default);

    /// <summary>Several providers' card graphs at once. The N+1 guard for a bookmark list.</summary>
    Task<IReadOnlyList<Provider>> GetManyAsync(
        IReadOnlyCollection<ProviderId> ids, CancellationToken cancellationToken = default);
}

/// <summary>
/// Who owns a provider and who may act for one.
/// <para>
/// The ownership and role questions authorization asks, plus the member's own provider list.
/// Separate because it changes when the membership model changes — a new role, a new limit on how
/// many a member may own — and that is unrelated to how a provider is stored.
/// </para>
/// </summary>
public interface IProviderMembership
{
    /// <summary>How many providers a member owns. Enforces the per-owner ceiling.</summary>
    Task<int> CountOwnedByAsync(UserId ownerId, CancellationToken cancellationToken = default);

    /// <summary>Every provider a member holds any role on. The "My Listings" screen.</summary>
    Task<IReadOnlyList<Provider>> ListForMemberAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// The roles a member holds across many providers, in one query.
    /// <para>
    /// Exists because a discovery page has to know whether the viewer owns each card — the
    /// client's provider screen greys out its own "Call" and "Message" buttons for an owner —
    /// and loading twenty aggregates to read twenty roles is the shape that makes a list slow.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ProviderRole>> GetRolesForAsync(
        UserId userId,
        IReadOnlyCollection<ProviderId> providerIds,
        CancellationToken cancellationToken = default);

    /// <summary>The owners of many providers, in one query. For self-review and self-report checks.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetOwnersAsync(
        IReadOnlyCollection<Guid> providerIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// How many providers are in a place or a category.
/// <para>
/// What B4's resource counters and the category facets ask for.
/// <see cref="QueryNearestCandidatesAsync"/> is declared here as well as on the search interface,
/// because "what is near this point" is the same question whether a directory screen or an area
/// page is asking it.
/// </para>
/// </summary>
public interface IProviderStatistics
{
    /// <summary>Active providers in a catalogue subtree. B4's area count.</summary>
    Task<int> CountInAreaAsync(
        string hierarchyPathPrefix, CancellationToken cancellationToken = default);

    /// <summary>Active providers in a country. B4's country count.</summary>
    Task<int> CountInCountryAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>Active providers per category, for refreshing the denormalised chip counts.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByCategoryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every match as identifiers and positions only, up to <paramref name="limit"/>. For a
    /// "nearest first" search, which orders by a distance only Location may measure.
    /// </summary>
    Task<IReadOnlyList<ProviderNearestCandidate>> QueryNearestCandidatesAsync(
        ProviderQueryFilter filter, int limit, CancellationToken cancellationToken = default);
}
