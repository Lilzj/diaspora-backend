using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an interview is called off.
/// <para>
/// Urgent by nature — a candidate who does not get this message travels to an appointment that
/// is not happening, which for a cross-border candidate can mean a wasted day and a wasted
/// fare. It is the one notification in the module worth delivering on every channel available.
/// </para>
/// </summary>
public sealed record InterviewCancelledDomainEvent : DomainEvent
{
    public required InterviewId InterviewId { get; init; }

    public required JobApplicationId ApplicationId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    /// <summary>When it was going to be. The message needs it to be unambiguous.</summary>
    public required DateTimeOffset WasScheduledAt { get; init; }

    public string? Reason { get; init; }

    public UserId? CancelledBy { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }
}
