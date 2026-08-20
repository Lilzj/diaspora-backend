namespace Diaspora.Modules.Jobs.Domain.Interviews;

/// <summary>One interviewer as the scheduling form sends them.</summary>
public sealed record InterviewParticipantDraft
{
    public Guid ParticipantId { get; init; }

    /// <summary>Optional: many interviewers are not platform members.</summary>
    public Guid? UserId { get; init; }

    public required string? Name { get; init; }

    public string? Role { get; init; }
}
