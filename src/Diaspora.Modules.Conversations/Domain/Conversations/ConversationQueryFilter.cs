using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Conversations.Domain.Conversations;

/// <summary>
/// What an inbox query is asking for.
/// <para>
/// <b>The filter is the client's <c>InboxFilter</c> decomposed, not transcribed.</b> The client has
/// thirteen chips — All, Unread, Requests, VerifiedOnly, Providers, Marketplace, Housing, Jobs,
/// Community, Organizations, Events, Support, Archived — and eight of them are the same query with a
/// different context type. Transcribing the enum would put <c>case "Housing"</c> in a repository,
/// which is precisely the vertical knowledge this module must not hold; the endpoint translates a
/// chip into <see cref="ContextTypes"/> and the repository never learns Housing exists.
/// </para>
/// <para>
/// <see cref="VerifiedOnly"/> is the one chip this module genuinely cannot serve: verification is
/// Trust's, and this module holds no trust marks. It is carried here so the query handler can apply
/// it through the participant directory port when a deployment registers one, and report it
/// unapplied when nothing does — silently ignoring it would show a member unverified contacts under
/// a "Verified only" heading.
/// </para>
/// </summary>
public sealed record ConversationQueryFilter
{
    public required UserId UserId { get; init; }

    /// <summary>
    /// Archived conversations are excluded unless this is true, and then they are all that is
    /// returned. There is no "both": the client's inbox and its archive are two screens.
    /// </summary>
    public bool ArchivedOnly { get; init; }

    public bool UnreadOnly { get; init; }

    public bool MutedOnly { get; init; }

    public bool PinnedOnly { get; init; }

    /// <summary>Pending requests only — the client's "Requests" chip and its separate screen.</summary>
    public bool RequestsOnly { get; init; }

    /// <summary>Catalogue keys. Empty means every type.</summary>
    public IReadOnlyList<string> ContextTypes { get; init; } = [];

    /// <summary>A specific external entity — every conversation about one listing.</summary>
    public string? EntityId { get; init; }

    /// <summary>Matched against the context title and the last message. Never against message bodies.</summary>
    public string? Search { get; init; }

    /// <summary>Requested by the client; applied only if a participant directory can answer it.</summary>
    public bool VerifiedOnly { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = ConversationLimits.DefaultPageSize;

    /// <summary>Clamped rather than rejected: an over-large page is a client bug, not a member's.</summary>
    public int EffectiveLimit => Limit switch
    {
        < 1 => ConversationLimits.DefaultPageSize,
        > ConversationLimits.MaxPageSize => ConversationLimits.MaxPageSize,
        _ => Limit
    };
}
