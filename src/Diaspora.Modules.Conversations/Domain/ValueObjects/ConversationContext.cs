using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// What the conversation is about, frozen at the moment it opened.
/// <para>
/// <b>A snapshot, and every field in it is deliberately dead.</b> The listing this conversation is
/// about will change price, sell, and eventually be deleted; the job will be filled; the event will
/// pass. None of that may alter what the thread says it was about. A buyer who reads "Toyota Corolla
/// — £4,200" three months later is reading what they were discussing, not what the seller has since
/// re-listed it for. That is why this holds a copy rather than a join, and it is the one place in
/// this module where duplicating another module's data is correct.
/// </para>
/// <para>
/// <b>The live status is not here and must not be.</b> The client's
/// <c>currentEntityStatus</c>/<c>currentEntityStatusNote</c> are documented in its own types as
/// "fetched separately, not snapshot", and this module honours that: they are resolved on the way
/// out through <see cref="Extensibility.IConversationContextResolver"/>, whose shipped
/// implementation returns nothing. Storing them would be duplicating data that has an owner, and
/// the copy would be wrong within minutes.
/// </para>
/// <para>
/// <b><c>EntityId</c> is a string, not a Guid.</b> Every identifier in this platform is a Guid
/// today, and the first vertical that references something by slug or by external key would need a
/// migration. A conversation stores a reference it never dereferences, so it costs nothing to keep
/// the reference opaque.
/// </para>
/// </summary>
public sealed class ConversationContext : ValueObject
{
    public const int MaxEntityIdLength = 128;
    public const int MaxTitleLength = 200;
    public const int MaxSubtitleLength = 200;
    public const int MaxPriceLength = 64;
    public const int MaxStatusLength = 64;
    public const int MaxDeepLinkLength = 512;

    private ConversationContext(
        ConversationType type,
        string entityId,
        string entityTitle,
        string? entitySubtitle,
        Guid? entityImageMediaId,
        string? entityPrice,
        string? entityStatus,
        string? entityStatusLabel,
        string? deepLink,
        DateTimeOffset snapshotAt)
    {
        Type = type;
        EntityId = entityId;
        EntityTitle = entityTitle;
        EntitySubtitle = entitySubtitle;
        EntityImageMediaId = entityImageMediaId;
        EntityPrice = entityPrice;
        EntityStatus = entityStatus;
        EntityStatusLabel = entityStatusLabel;
        DeepLink = deepLink;
        SnapshotAt = snapshotAt;
    }

    private ConversationContext() { }

    public ConversationType Type { get; private set; } = default!;

    public string EntityId { get; private set; } = default!;

    public string EntityTitle { get; private set; } = default!;

    public string? EntitySubtitle { get; private set; }

    /// <summary>
    /// A B5 asset identifier, never a URL.
    /// <para>
    /// ADR-005, and the client's field is <c>entityImageUrl</c> — the mapping is recorded in the
    /// frontend compatibility report. The response carries both: this identifier always, and a
    /// resolved URL only when a deployment registers something that can resolve one.
    /// </para>
    /// </summary>
    public Guid? EntityImageMediaId { get; private set; }

    /// <summary>The formatted price as the client displayed it. A string, because it is display.</summary>
    public string? EntityPrice { get; private set; }

    public string? EntityStatus { get; private set; }

    public string? EntityStatusLabel { get; private set; }

    public string? DeepLink { get; private set; }

    public DateTimeOffset SnapshotAt { get; private set; }

    public static ConversationContext Create(
        ConversationType type,
        string entityId,
        string entityTitle,
        DateTimeOffset snapshotAt,
        string? entitySubtitle = null,
        Guid? entityImageMediaId = null,
        string? entityPrice = null,
        string? entityStatus = null,
        string? entityStatusLabel = null,
        string? deepLink = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        return new ConversationContext(
            type,
            Clamp(entityId, MaxEntityIdLength)!,

            // An empty title is tolerated rather than refused. The client passes whatever heading it
            // rendered, and a conversation that opened from a screen with no heading is a
            // conversation, not an error — refusing it would lose the message somebody typed.
            Clamp(entityTitle, MaxTitleLength) ?? string.Empty,
            Clamp(entitySubtitle, MaxSubtitleLength),
            entityImageMediaId == Guid.Empty ? null : entityImageMediaId,
            Clamp(entityPrice, MaxPriceLength),
            Clamp(entityStatus, MaxStatusLength),
            Clamp(entityStatusLabel, MaxStatusLength),
            Clamp(deepLink, MaxDeepLinkLength),
            snapshotAt);
    }

    /// <summary>
    /// The reference this context points at, for a caller that only needs the pair.
    /// </summary>
    public ConversationReference ToReference() => ConversationReference.Create(Type, EntityId);

    /// <summary>Whether this context names the same external thing as another.</summary>
    public bool References(ConversationType type, string entityId) =>
        type is not null
        && Type.Equals(type)
        && string.Equals(EntityId, entityId, StringComparison.Ordinal);

    private static string? Clamp(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return EntityId;
        yield return EntityTitle;
        yield return EntitySubtitle;
        yield return EntityImageMediaId;
        yield return EntityPrice;
        yield return EntityStatus;
        yield return EntityStatusLabel;
        yield return DeepLink;
        yield return SnapshotAt;
    }
}
