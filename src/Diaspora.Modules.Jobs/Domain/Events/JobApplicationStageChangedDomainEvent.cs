using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an application moves between the employer's own pipeline stages.
/// <para>
/// Distinct from a status change, and the distinction is real: status is the platform's
/// vocabulary and the same for everyone, while a stage is the employer's — "Phone screen",
/// "Panel", "Ops sign-off". An application can move from one custom stage to another without
/// its status changing at all, and the employer's funnel chart is built from these, not from
/// statuses.
/// </para>
/// </summary>
public sealed record JobApplicationStageChangedDomainEvent : DomainEvent
{
    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    /// <summary>Null when the application is entering the pipeline for the first time.</summary>
    public string? PreviousStageKey { get; init; }

    public required string CurrentStageKey { get; init; }

    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
