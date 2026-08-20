using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a posting's moderation standing changes — flagged, hidden, or cleared.
/// <para>
/// The search index cares about exactly one thing here: a hidden posting must leave it
/// immediately. Fraudulent job adverts are the single most damaging content this platform can
/// carry, and the gap between a moderator hiding one and the index forgetting it is the window
/// in which someone loses money.
/// </para>
/// </summary>
public sealed record JobPostingModerationChangedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required JobModerationState PreviousState { get; init; }

    public required JobModerationState CurrentState { get; init; }

    /// <summary>Null when a report raised the flag rather than a moderator acting.</summary>
    public UserId? ModeratorId { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
