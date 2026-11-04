using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Conversations.Extensibility;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Catalogue
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// What a vertical contributes so its conversations can exist.
/// <para>
/// <b>This is the seam that keeps "never hardcode vertical logic" true.</b> A new vertical ships an
/// implementation from its own assembly, the seeder finds it by scan, and this module never learns
/// the vertical's name. Nothing here is required for the twelve types the client already knows —
/// those are contributed by this module's own contributor, and a vertical that wants to change one
/// of them contributes the same key with different behaviour.
/// </para>
/// </summary>
public interface IConversationContextTypeContributor
{
    string Module { get; }

    IEnumerable<ConversationContextTypeSeed> GetContextTypes();
}

/// <summary>One catalogue row, as a contributor states it.</summary>
public sealed record ConversationContextTypeSeed
{
    /// <summary>The client's literal. Case-sensitive; it is what arrives on the wire.</summary>
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    /// <summary>Informational. Nothing branches on it.</summary>
    public string? OwningModule { get; init; }

    /// <summary>Whether a stranger opening one of these produces a request rather than a thread.</summary>
    public bool RequiresAcceptance { get; init; }

    public bool AllowsAttachments { get; init; } = true;

    /// <summary>Whether only the platform may open one.</summary>
    public bool IsSystemManaged { get; init; }

    public int Position { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// People
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Turns member identifiers into names, avatars and trust marks.
/// <para>
/// <b>The seam exists because this module resolves none of those and must not.</b> The client's
/// <c>ConversationParticipant</c> carries <c>displayName</c>, <c>avatarUrl</c> and a whole
/// <c>trust</c> object; producing them here would mean a project reference on Identity, Trust and
/// Media, three modules whose data this one has no business holding — and B13 already established
/// the precedent by refusing to put a display name on a typing signal.
/// </para>
/// <para>
/// <b>The shipped implementation returns identifiers and nothing else, and that is a complete
/// answer rather than a stub.</b> A deployment that wants names registers a resolver after this
/// module; the response shape does not change either way, so a client renders an identifier-only
/// participant exactly as it renders a fully populated one. The frontend compatibility report
/// records which fields are null until something is registered.
/// </para>
/// <para>
/// Consulted once per inbox page with every counterpart at once, never per row.
/// </para>
/// </summary>
public interface IConversationParticipantDirectory
{
    /// <summary>Whether anything more than identifiers can be produced. Read by the health contributor.</summary>
    bool IsAvailable { get; }

    Task<IReadOnlyDictionary<Guid, ConversationParticipantProfile>> ResolveAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a directory can say about somebody.
/// <para>
/// Every field but the identifier is optional, because every field but the identifier belongs to
/// another module. <see cref="AvatarMediaId"/> is a B5 asset rather than a URL, for the same reason
/// an attachment is.
/// </para>
/// </summary>
public sealed record ConversationParticipantProfile
{
    public required Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public Guid? AvatarMediaId { get; init; }

    /// <summary>
    /// Their profile photo, an address an image can load (added 4 Nov 2026). Every thread showed
    /// initials, the photo a member had set included.
    /// </summary>
    public string? AvatarUrl { get; init; }

    public bool IsVerified { get; init; }

    public string? VerificationLabel { get; init; }

    public decimal? Rating { get; init; }

    public int? ReviewCount { get; init; }

    public string? ResponseTime { get; init; }

    public int? ResponseRate { get; init; }

    public string? MemberSince { get; init; }

    public string? ReputationLevel { get; init; }

    public int? TotalHelpfulVotes { get; init; }

    public static ConversationParticipantProfile IdentifierOnly(Guid userId) => new() { UserId = userId };
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Context
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Answers what has become of the thing a conversation is about.
/// <para>
/// <b>This is how the client's <c>currentEntityStatus</c> is served without this module storing
/// it.</b> Its own type file says the field is "fetched separately, not snapshot" — so it is
/// resolved on the way out, from whoever owns the entity, and never written into the context. A
/// stored copy would be duplicating data with an owner, and would be wrong the first time a price
/// changed.
/// </para>
/// <para>
/// The shipped implementation answers nothing. A deployment that wants live status registers a
/// resolver that reads the owning modules; the fields stay null until it does, which is exactly what
/// the client already handles because they are optional in its own types.
/// </para>
/// </summary>
public interface IConversationContextResolver
{
    bool IsAvailable { get; }

    Task<IReadOnlyDictionary<string, ConversationContextStatus>> ResolveAsync(
        IReadOnlyCollection<ConversationReference> references,
        CancellationToken cancellationToken = default);
}

/// <summary>What an owning module says about one of its entities, right now.</summary>
public sealed record ConversationContextStatus
{
    /// <summary>The reference key this answers, spelled as <c>ConversationReference.ToString</c>.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// One of the client's literals: <c>Available</c>, <c>Sold</c>, <c>Rented</c>, <c>Filled</c>,
    /// <c>Cancelled</c>, <c>Unavailable</c>, <c>PriceChanged</c>, <c>Active</c>.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>A short human note — "Price reduced by £200 yesterday".</summary>
    public string? Note { get; init; }

    /// <summary>A resolved image URL for the snapshot, when the deployment can produce one.</summary>
    public string? ImageUrl { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Policy
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// An extra rule about whether one member may open a conversation with another.
/// <para>
/// <b>Additive and refusal-only.</b> Implementations can refuse; none of them can permit something
/// this module refused. That direction is what makes it safe to scan for them across every assembly:
/// a badly written policy costs a conversation, never a leak. Trust's account-level bans and a
/// deployment's own "verified members only" rule both arrive here.
/// </para>
/// </summary>
public interface IConversationPolicy
{
    string Name { get; }

    Task<ConversationPolicyDecision> EvaluateAsync(
        ConversationPolicyContext context, CancellationToken cancellationToken = default);
}

/// <summary>What a policy is asked about.</summary>
public sealed record ConversationPolicyContext
{
    public required UserId InitiatorUserId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string ContextTypeKey { get; init; }

    public required string EntityId { get; init; }
}

/// <summary>A policy's answer. <see cref="Allowed"/> is the only success.</summary>
public sealed record ConversationPolicyDecision
{
    public required bool Allowed { get; init; }

    public string? Reason { get; init; }

    public static ConversationPolicyDecision Allow() => new() { Allowed = true };

    public static ConversationPolicyDecision Refuse(string reason) =>
        new() { Allowed = false, Reason = reason };
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Search
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Searches inside conversations the caller is in.
/// <para>
/// <b>A port with a deliberately limited shipped implementation.</b> The EF implementation matches
/// message bodies with a LIKE across the caller's own conversations, which is correct and gets slow
/// somewhere in the tens of thousands of messages. A deployment that needs more registers an index
/// behind this interface. Indexing conversations into a shared search cluster is explicitly out of
/// scope for this phase — the port exists so that decision stays a deployment's, and so nobody is
/// tempted to reach for the platform's general search provider, which has no notion of who may read
/// what.
/// </para>
/// </summary>
public interface IConversationSearchEngine
{
    string Provider { get; }

    Task<IReadOnlyList<ConversationSearchHit>> SearchAsync(
        UserId searcher, string query, int limit, CancellationToken cancellationToken = default);
}

/// <summary>One match.</summary>
public sealed record ConversationSearchHit
{
    public required ConversationId ConversationId { get; init; }

    public required ConversationMessageId MessageId { get; init; }

    public required DateTimeOffset MatchedAt { get; init; }

    public string? Excerpt { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Attachments
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Decides whether an attachment may be sent.
/// <para>
/// The shipped implementation checks the feature flag and the context type's own
/// <c>AllowsAttachments</c>, and asserts nothing about the file — this module cannot, because Media
/// owns the asset and has already scanned it. A deployment with stricter rules (no documents in
/// support tickets, no video below a trust level) registers its own.
/// </para>
/// </summary>
public interface IConversationAttachmentValidator
{
    Task<ConversationPolicyDecision> ValidateAsync(
        Conversation conversation,
        AttachmentReference attachment,
        CancellationToken cancellationToken = default);
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Message shaping
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A last look at a message before it is stored.
/// <para>
/// Where moderation, link scrubbing or profanity handling would attach. Nothing ships behind it: an
/// implementation that silently rewrote what somebody typed is a decision a platform makes
/// deliberately, and it is not this phase's to make.
/// </para>
/// </summary>
public interface IConversationMessageInspector
{
    string Name { get; }

    Task<ConversationPolicyDecision> InspectAsync(
        Conversation conversation,
        ConversationMessage message,
        CancellationToken cancellationToken = default);
}
