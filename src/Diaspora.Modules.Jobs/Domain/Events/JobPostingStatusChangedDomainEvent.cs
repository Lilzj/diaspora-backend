using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised on every move through the posting lifecycle.
/// <para>
/// <b>One event carrying both ends, rather than nine events named after their destinations.</b>
/// A consumer almost always cares about the transition rather than the state — de-index when a
/// posting leaves public visibility, notify applicants when it reaches Filled — and a
/// from/to pair answers that in one subscription. It is also what lets the relay publish the
/// brief's separate <c>JobClosed</c> and <c>JobArchived</c> contracts from a single domain fact
/// without the aggregate knowing those contracts exist.
/// </para>
/// <para>
/// <see cref="ChangedBy"/> is null when the clock did it. Expiry has no actor, and attributing
/// it to whoever last touched the posting would put a person's name against a decision they did
/// not make.
/// </para>
/// </summary>
public sealed record JobPostingStatusChangedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required JobStatus PreviousStatus { get; init; }

    public required JobStatus CurrentStatus { get; init; }

    /// <summary>Null when the transition was the clock's rather than a person's.</summary>
    public UserId? ChangedBy { get; init; }

    /// <summary>Why, where the employer gave a reason. Shown to applicants on a closure.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
