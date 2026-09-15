using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// Reads and writes providers.
/// <para>
/// <b>Three shapes of read, and the split is load-bearing.</b> <see cref="GetAsync"/>
/// materialises the whole aggregate — members, services, areas, hours, gallery, certifications,
/// verifications — because a command about to mutate it needs all of it and a partially loaded
/// aggregate cannot enforce its own invariants. <see cref="GetForReadAsync"/> materialises the
/// profile-screen graph. <see cref="QueryAsync"/> materialises the <em>card</em> graph, because
/// a discovery page of twenty cards that eagerly loaded every provider's whole gallery, service
/// list and weekly schedule would be several thousand rows to render twenty names.
/// </para>
/// <para>
/// The query method takes a <see cref="ProviderQueryFilter"/> rather than an
/// <c>IQueryable</c>, for the reason that type documents: a contributed ranking strategy must
/// not be able to compose an expression that fails to translate at run time inside somebody
/// else's stack trace.
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
    Task<IReadOnlyDictionary<Guid, Enums.ProviderRole>> GetRolesForAsync(
        UserId userId,
        IReadOnlyCollection<ProviderId> providerIds,
        CancellationToken cancellationToken = default);

    /// <summary>The owners of many providers, in one query. For self-review and self-report checks.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetOwnersAsync(
        IReadOnlyCollection<Guid> providerIds, CancellationToken cancellationToken = default);

    /// <summary>Active providers in a catalogue subtree. B4's area count.</summary>
    Task<int> CountInAreaAsync(
        string hierarchyPathPrefix, CancellationToken cancellationToken = default);

    /// <summary>Active providers in a country. B4's country count.</summary>
    Task<int> CountInCountryAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>Active providers per category, for refreshing the denormalised chip counts.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByCategoryAsync(
        CancellationToken cancellationToken = default);

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
