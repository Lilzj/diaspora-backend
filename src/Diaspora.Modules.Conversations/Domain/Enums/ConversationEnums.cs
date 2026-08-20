namespace Diaspora.Modules.Conversations.Domain.Enums;

/// <summary>
/// Where a conversation is in its own life, independent of any one participant.
/// <para>
/// <b>Three values, not seven, and the difference from the client's <c>ConversationState</c> is the
/// most important modelling decision in this module.</b> The client has one field carrying
/// <c>PendingRequest | Accepted | Active | Muted | Archived | Blocked | Closed</c>. Four of those
/// are not facts about the conversation at all — they are facts about one member's relationship to
/// it. If A archives a conversation and B does not, there is no single value the row can hold. A
/// backend that stored the client's enum verbatim would have to pick a winner, and whoever archived
/// last would silently archive it for the other person.
/// </para>
/// <para>
/// So the aggregate holds what is genuinely shared — has the request been accepted, is it closed —
/// and archive, mute and pin live on <c>ConversationParticipant</c>. The client's single field is
/// <em>computed per viewer</em> on the way out, which is why the wire contract is unchanged and the
/// storage is correct. See <c>ConversationStatePolicy</c> for the precedence.
/// </para>
/// </summary>
public enum ConversationStatus
{
    /// <summary>Opened by somebody the recipient has no prior relationship with; awaiting acceptance.</summary>
    PendingRequest = 0,

    /// <summary>Ordinary and ongoing.</summary>
    Active = 1,

    /// <summary>Permanently ended — the listing was removed, the job was filled, a moderator closed it.</summary>
    Closed = 2
}

/// <summary>
/// The client's single-field view of a conversation, computed per viewer.
/// <para>
/// Spelled exactly as <c>src/types/conversation.ts</c> spells it, because it is that contract. This
/// enum is never persisted: it is produced by <c>ConversationStatePolicy</c> from the aggregate's
/// status, the viewer's participant row and the block list, and it exists as a type so a mapper
/// cannot invent a value the client does not handle.
/// </para>
/// </summary>
public enum ConversationState
{
    PendingRequest = 0,
    Accepted = 1,
    Active = 2,
    Muted = 3,
    Archived = 4,
    Blocked = 5,
    Closed = 6
}

/// <summary>
/// Why somebody is in a conversation.
/// <para>
/// <b>Not "buyer" and "seller".</b> Naming the roles after one vertical is exactly the hardcoding
/// this module must not do — Housing would need landlord and tenant, Jobs employer and candidate,
/// and the enum would grow a pair per vertical forever. <see cref="Initiator"/> and
/// <see cref="Recipient"/> carry the only distinction the platform actually acts on: who started it,
/// which is who a pending request is waiting on. A vertical that needs richer roles reads them from
/// the context entity it owns.
/// </para>
/// </summary>
public enum ParticipantRole
{
    /// <summary>Opened the conversation.</summary>
    Initiator = 0,

    /// <summary>Was opened to.</summary>
    Recipient = 1,

    /// <summary>Added later — a colleague brought into an enquiry, a moderator observing.</summary>
    Observer = 2
}

/// <summary>
/// What kind of thing a message is.
/// <para>
/// The lower-case wire spellings the client uses — <c>text</c>, <c>image</c>, <c>voice</c>,
/// <c>document</c>, <c>system</c>, <c>verification</c> — are produced by <c>ConversationWireNames</c>
/// rather than by serialising this enum, so a C# rename cannot silently break a shipped build. The
/// four kinds beyond the client's list are additive: a client that receives one it does not know
/// renders the body text, which every kind carries.
/// </para>
/// </summary>
public enum MessageKind
{
    Text = 0,
    Image = 1,
    Voice = 2,
    Document = 3,

    /// <summary>Written by the platform, attributable to nobody, editable by nobody.</summary>
    System = 4,

    /// <summary>A trust event surfaced in the thread — "Amara is now Diaspora Verified".</summary>
    Verification = 5,

    Video = 6,
    Location = 7,

    /// <summary>A reference to something the Financial Platform owns. Never an amount.</summary>
    FinancialEvent = 8,

    /// <summary>Reserved. No producer ships in this phase; see the module description.</summary>
    Assistant = 9
}

/// <summary>
/// How far a message has travelled.
/// <para>
/// <b>The server never produces <c>queued</c>, <c>sending</c> or <c>failed</c>.</b> Those three of
/// the client's six are states of the client's own outbox — a message the server has never seen. The
/// server's first knowledge of a message is <see cref="Sent"/>, and a client that received a
/// server-issued "sending" would have two sources of truth for a state only it can observe.
/// </para>
/// </summary>
public enum MessageStatus
{
    /// <summary>The server has it. One tick.</summary>
    Sent = 0,

    /// <summary>A recipient's device acknowledged it. Two ticks.</summary>
    Delivered = 1,

    /// <summary>A recipient opened the conversation past it. Blue.</summary>
    Read = 2,

    /// <summary>Withdrawn by its sender. The row stays; the body does not.</summary>
    Deleted = 3,

    /// <summary>Withheld by moderation. Distinct from deleted: the sender did not choose it.</summary>
    Hidden = 4
}

/// <summary>What a member attached. Mirrors the client's <c>AttachmentType</c>.</summary>
public enum AttachmentKind
{
    Image = 0,
    Document = 1,
    Voice = 2,
    Video = 3,
    Location = 4,
    Contact = 5
}

/// <summary>
/// The four mute lengths the client offers.
/// <para>
/// An enum rather than a duration, because the client sends <c>'8h' | '24h' | '7d' | 'forever'</c>
/// and a server that accepted arbitrary spans would be accepting a contract nothing produces —
/// and would have to decide what a 400-year mute means.
/// </para>
/// </summary>
public enum MuteDuration
{
    EightHours = 0,
    OneDay = 1,
    SevenDays = 2,
    Forever = 3
}

/// <summary>
/// Why the platform wrote a message nobody sent.
/// <para>
/// A stable key rather than the rendered sentence: the sentence is localised at the edge, and a
/// system message written in English in 2026 must still render in Wolof in 2028.
/// </para>
/// </summary>
public enum SystemMessageKind
{
    ConversationStarted = 0,
    RequestAccepted = 1,
    ParticipantAdded = 2,
    ParticipantRemoved = 3,
    ContextClosed = 4,
    ConversationClosed = 5,
    ConversationRestored = 6
}
