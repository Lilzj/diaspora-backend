using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Integration;

/// <summary>
/// The read surface other modules use to look providers up.
/// <para>
/// <b>This is the contract that makes the brief's central promise real</b> — "future modules
/// must never implement their own provider model". A Marketplace listing sold by a shop, a Job
/// posted by an employer, a Housing listing let by an agency and an Event hosted by an
/// association are all "a provider did this", and each of those modules stores a
/// <see cref="ProviderId"/> and asks here rather than growing a provider table of its own.
/// </para>
/// <para>
/// Deliberately narrower than <c>IProviderRepository</c>. A consuming vertical needs to resolve
/// a provider, render a card, check a role and confirm a badge; it does not need to publish one,
/// suspend one or rewrite its opening hours, and handing it the repository would give it all
/// three. The narrow interface is also the one that can stay stable if the storage behind it
/// changes.
/// </para>
/// <para>
/// Every method is batched or single-fetch by design. There is no "get all providers" — a
/// vertical that wants to iterate the directory is about to do something B7 should be doing for
/// it.
/// </para>
/// </summary>
public interface IProviderDirectory
{
    /// <summary>One provider, or null when it does not exist or is not visible.</summary>
    Task<ProviderSummary?> FindAsync(
        ProviderId providerId, CancellationToken cancellationToken = default);

    /// <summary>One provider by its deep-link handle.</summary>
    Task<ProviderSummary?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several providers at once. The N+1 guard for any screen rendering a list of things that
    /// each name a provider — which is every listing screen in every future vertical.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ProviderSummary>> FindManyAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a member may act on behalf of a provider at a given level of authority.
    /// <para>
    /// The one authorisation question every consuming vertical asks: may this person post a job
    /// as this employer, list an item as this shop, publish an event as this association. Asking
    /// here rather than reimplementing role comparison is what stops five modules disagreeing
    /// about whether an editor may post.
    /// </para>
    /// </summary>
    Task<bool> HasRoleAsync(
        UserId userId,
        ProviderId providerId,
        ProviderRole minimumRole,
        CancellationToken cancellationToken = default);

    /// <summary>Every provider a member may act for, at or above a role. Powers an "acting as" picker.</summary>
    Task<IReadOnlyList<ProviderSummary>> ListForMemberAsync(
        UserId userId,
        ProviderRole minimumRole = ProviderRole.Editor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a provider holds a current verification of a kind.
    /// <para>
    /// Reads B7's projection rather than calling Trust, exactly as B4's residency directory does.
    /// A stale projection can only be stale in the direction of a decision Trust has already made
    /// and B7 has not yet heard — bounded by event delivery, not unbounded.
    /// </para>
    /// </summary>
    Task<bool> IsVerifiedAsync(
        ProviderId providerId,
        ProviderVerificationKind? kind = null,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the provider is open right now. Null when the question does not apply.</summary>
    Task<bool?> IsOpenNowAsync(
        ProviderId providerId, CancellationToken cancellationToken = default);
}
