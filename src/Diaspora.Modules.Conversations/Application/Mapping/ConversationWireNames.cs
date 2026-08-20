using Diaspora.Modules.Conversations.Domain.Enums;

namespace Diaspora.Modules.Conversations.Application.Mapping;

/// <summary>
/// Translates between this module's enums and the strings the client sends and reads.
/// <para>
/// <b>Explicit tables rather than <c>ToString()</c>, and that is not ceremony.</b> The client spells
/// message types in lower case (<c>text</c>, <c>image</c>) and conversation states in Pascal case
/// (<c>PendingRequest</c>, <c>Active</c>), because those are two different vocabularies that grew in
/// two different files. Serialising an enum would produce one casing for both and quietly break
/// whichever one it did not match — with no compiler error, because a string is a string. A table
/// makes the mapping a decision somebody can read, and makes a C# rename a compile error rather than
/// a shipped-app failure.
/// </para>
/// <para>
/// Parsing is forgiving in one direction only: unknown input becomes a sensible default, never an
/// exception, because the input is a mobile client that may be three versions old. Unknown
/// <em>output</em> cannot happen — every enum member has a case.
/// </para>
/// </summary>
public static class ConversationWireNames
{
    // ── Conversation state ──────────────────────────────────────────────────────────────────────

    public static string ToWire(ConversationState state) => state switch
    {
        ConversationState.PendingRequest => "PendingRequest",
        ConversationState.Accepted => "Accepted",
        ConversationState.Muted => "Muted",
        ConversationState.Archived => "Archived",
        ConversationState.Blocked => "Blocked",
        ConversationState.Closed => "Closed",
        _ => "Active"
    };

    // ── Message kind ────────────────────────────────────────────────────────────────────────────

    public static string ToWire(MessageKind kind) => kind switch
    {
        MessageKind.Image => "image",
        MessageKind.Voice => "voice",
        MessageKind.Document => "document",
        MessageKind.System => "system",
        MessageKind.Verification => "verification",
        MessageKind.Video => "video",
        MessageKind.Location => "location",
        MessageKind.FinancialEvent => "financial",
        MessageKind.Assistant => "assistant",
        _ => "text"
    };

    public static MessageKind? ParseMessageKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "text" => MessageKind.Text,
        "image" => MessageKind.Image,
        "voice" => MessageKind.Voice,
        "document" => MessageKind.Document,
        "video" => MessageKind.Video,
        "location" => MessageKind.Location,
        "financial" => MessageKind.FinancialEvent,

        // Never parsed from a client. A member cannot ask for the platform's own voice; see
        // ConversationMessage.Send, which downgrades it to text if one somehow arrives.
        "system" => MessageKind.Text,
        "verification" => MessageKind.Text,
        _ => null
    };

    // ── Message status ──────────────────────────────────────────────────────────────────────────

    public static string ToWire(MessageStatus status) => status switch
    {
        MessageStatus.Delivered => "delivered",
        MessageStatus.Read => "read",

        // A withdrawn or hidden message keeps its last real delivery state on the wire: the client's
        // MessageStatus has no "deleted" member — deletion is `isDeleted`, a separate boolean it
        // already renders. Inventing a seventh status would be inventing a DTO.
        MessageStatus.Deleted or MessageStatus.Hidden => "sent",
        _ => "sent"
    };

    // ── Attachment kind ─────────────────────────────────────────────────────────────────────────

    public static string ToWire(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Document => "document",
        AttachmentKind.Voice => "voice",
        AttachmentKind.Video => "video",
        AttachmentKind.Location => "location",
        AttachmentKind.Contact => "contact",
        _ => "image"
    };

    public static AttachmentKind? ParseAttachmentKind(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "image" => AttachmentKind.Image,
            "document" => AttachmentKind.Document,
            "voice" => AttachmentKind.Voice,
            "video" => AttachmentKind.Video,
            "location" => AttachmentKind.Location,
            "contact" => AttachmentKind.Contact,
            _ => null
        };

    // ── Participant role ────────────────────────────────────────────────────────────────────────

    public static string ToWire(ParticipantRole role) => role switch
    {
        ParticipantRole.Initiator => "Initiator",
        ParticipantRole.Observer => "Observer",
        _ => "Recipient"
    };

    // ── Mute duration ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the client's four literals. Anything else is treated as absent, which the caller turns
    /// into an unmute — the same thing the client sends deliberately as <c>duration: null</c>.
    /// </summary>
    public static MuteDuration? ParseMuteDuration(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "8h" => MuteDuration.EightHours,
            "24h" => MuteDuration.OneDay,
            "7d" => MuteDuration.SevenDays,
            "forever" => MuteDuration.Forever,
            _ => null
        };

    /// <summary>
    /// When a mute of this length ends, or null for indefinitely.
    /// </summary>
    public static DateTimeOffset? MutedUntil(MuteDuration duration, DateTimeOffset now) => duration switch
    {
        MuteDuration.EightHours => now.AddHours(8),
        MuteDuration.OneDay => now.AddDays(1),
        MuteDuration.SevenDays => now.AddDays(7),
        _ => null
    };

    // ── Inbox filters ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Turns one of the client's thirteen filter chips into catalogue keys.
    /// <para>
    /// <b>This is the only place in the module that names a vertical, and it names them as data.</b>
    /// The chips are the client's vocabulary and the client is not going to change; mapping them
    /// here — at the edge, in one function — keeps every layer beneath it filtering on catalogue
    /// keys it never interprets. A chip whose keys are not in the catalogue returns nothing, which
    /// is the honest answer for a vertical that is not deployed.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ContextTypesForFilter(string? filter) =>
        filter?.Trim().ToLowerInvariant() switch
        {
            "providers" => [Domain.ValueObjects.ConversationTypeKeys.ProviderInquiry],
            "marketplace" => [Domain.ValueObjects.ConversationTypeKeys.MarketplaceListing],
            "housing" => [Domain.ValueObjects.ConversationTypeKeys.HousingListing],
            "jobs" => [Domain.ValueObjects.ConversationTypeKeys.JobOpportunity],
            "community" or "events" => [Domain.ValueObjects.ConversationTypeKeys.CommunityEvent],
            "organizations" => [Domain.ValueObjects.ConversationTypeKeys.Organization],
            "support" => [Domain.ValueObjects.ConversationTypeKeys.SupportTicket],
            _ => []
        };
}
