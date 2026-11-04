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
/// Deliberately thin: a name to address them by and their photo, which is what a list of threads
/// or a page of comments needs, and whether their account is verified. Anything more belongs to
/// the module that owns the question — standing to <see cref="ITrustService"/>, reputation to
/// Trust's own contracts.
/// </para>
/// </summary>
public sealed record MemberProfile
{
    public required Guid UserId { get; init; }

    /// <summary>What to call them. Never empty for a member that exists.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Their profile photo, an absolute address an image tag can load. Null when they have none.
    /// <para>
    /// Added 4 Nov 2026. A member who set a photo still showed as initials beside every post,
    /// comment, guide and message, because the name travelled here and the photo did not. It
    /// once could not: Identity stored a free-text key. Since 20 Oct 2026 it stores a Media
    /// asset, and the host turns that into the address <c>/auth/me</c> already serves.
    /// </para>
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Whether their account is verified right now — the access tier the platform gates verified
    /// actions on, granted only by the Trust Engine.
    /// <para>
    /// Added 14 Sep 2026 so an employer can see who applied as the platform knows them. It is the
    /// account's present state, not a record of the moment they acted: a suspension or a withdrawn
    /// verification turns it false.
    /// </para>
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// The ISO 3166-1 alpha-2 country they live in, as they registered or last updated it.
    /// <para>
    /// Added 14 Oct 2026 because a member's currency comes from where they live, never from a
    /// platform default: Financial and Creator answered GBP to a member resident in Mauritius
    /// whenever they had no money yet to take a currency from. Null when the identity store has no
    /// country for them, which a caller must treat as "unknown", not as any particular country.
    /// </para>
    /// </summary>
    public string? CountryOfResidence { get; init; }
}
