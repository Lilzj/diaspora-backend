using Diaspora.Domain.Identity.AccessStates;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// The Access Gateway's verdict for one member at one moment: where to send them, what
/// they can do, and what would move them forward.
/// </summary>
public sealed record AccessDecision
{
    public required AccessState AccessState { get; init; }

    public required AccountStatus AccountStatus { get; init; }

    public required AccessDestination Destination { get; init; }

    /// <summary>Member-facing explanation of the current position.</summary>
    public required string Message { get; init; }

    /// <summary>What the member must do to progress, in the order to do it.</summary>
    public required IReadOnlyList<string> RequiredActions { get; init; }

    public required FeatureAccessSnapshot Features { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>Set when a restriction lifts on a known date.</summary>
    public DateTimeOffset? RestrictedUntil { get; init; }

    /// <summary>The moderation reason code staff chose, for a suspended or closed account.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>That reason as the member reads it.</summary>
    public string? ReasonLabel { get; init; }

    /// <summary>What staff wrote, for a suspended or closed account.</summary>
    public string? Note { get; init; }
}
