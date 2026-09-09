using Diaspora.SharedKernel.Identity;

namespace Diaspora.Platform.Services.Abstractions;

/// <summary>
/// The little a module needs to know about a member it did not create.
/// <para>
/// <b>The gap this closes.</b> B13 shipped <c>IConversationParticipantDirectory</c> with a
/// deliberately incomplete default — <c>IdentifierOnlyParticipantDirectory</c>, which answers with
/// an identifier and nothing else — and no module ever supplied the real one. So every conversation
/// on the platform came back with a counterparty carrying no name, and the client, reading
/// <c>displayName</c> and finding it null, rendered every thread as <em>"Unknown"</em>. Not
/// organization threads: <em>every</em> thread, including a member messaging a seller about a
/// listing.
/// </para>
/// <para>
/// <b>Why a kernel port and not a project reference.</b> Identity's users live below the modules,
/// and Conversations must not reach down into them; the same reasoning is written out on
/// <c>AccountStandingPolicies</c>, which reads <see cref="ITrustService"/> for exactly this reason
/// and gains no reference to anything. This is that port for "who is this member", and the identity
/// layer implements it.
/// </para>
/// <para>
/// <b>Batch, not one at a time.</b> A page of twenty conversations resolves in one call. A port
/// shaped around a single member is a port that will be called inside a loop.
/// </para>
/// </summary>
public interface IMemberProfileService
{
    /// <summary>
    /// Whether this service can answer with more than the identifiers it was given.
    /// <para>
    /// A caller must distinguish "this member does not exist" from "I cannot tell you". The
    /// placeholder says false and returns nothing, so a caller that refuses on absence would refuse
    /// everybody — a request that was never answered is not a no.
    /// </para>
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// The members among these identifiers, keyed by identifier.
    /// <para>
    /// An identifier with no member is simply absent from the result. That absence is the only
    /// honest answer to "is this a person", and it is what lets a caller refuse a conversation
    /// addressed to something that is not one.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MemberProfile>> ResolveAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What the kernel is willing to say about a member to a module that did not create them.
/// <para>
/// Deliberately thin: a name to address them by, which is what a list of threads or a page of
/// comments needs. No avatar, because identity stores one as a storage key rather than a B5 media
/// identifier and a field nothing can fill is worse than a field that is not there. Anything more
/// belongs to the module that owns the question — standing to <see cref="ITrustService"/>,
/// reputation to Trust's own contracts.
/// </para>
/// </summary>
public sealed record MemberProfile
{
    public required Guid UserId { get; init; }

    /// <summary>What to call them. Never empty for a member that exists.</summary>
    public required string DisplayName { get; init; }
}
