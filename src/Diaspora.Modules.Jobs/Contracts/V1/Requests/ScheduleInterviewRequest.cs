namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Schedules an interview round against an application.
/// <para>
/// The <see cref="Mode"/> decides which detail is required: an on-site interview needs an
/// address, a remote one needs a joining link. A cross-border candidate told only "Tuesday at
/// ten" cannot tell whether they need a visa appointment, and catching that when the employer
/// schedules it is far better than an hour beforehand.
/// </para>
/// </summary>
public sealed record ScheduleInterviewRequest
{
    /// <summary><c>Phone</c>, <c>Video</c>, <c>OnSite</c>, <c>Panel</c>, <c>Technical</c>, <c>Assessment</c>.</summary>
    public required string Mode { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }

    public required int DurationMinutes { get; init; }

    /// <summary>
    /// The employer's IANA wall clock. Not redundant with the instant: the instant says
    /// unambiguously when, this says which clock they meant, and a candidate three time zones
    /// away needs both rendered.
    /// </summary>
    public string? TimeZoneId { get; init; }

    /// <summary>What the employer calls this round. "Panel with the ward team".</summary>
    public string? Title { get; init; }

    /// <summary>Required for an on-site interview.</summary>
    public string? LocationAddress { get; init; }

    /// <summary>Required for a phone, video, panel or technical interview.</summary>
    public string? JoiningUrl { get; init; }

    /// <summary>What to bring, who to ask for.</summary>
    public string? Instructions { get; init; }

    /// <summary>The panel. Many interviewers are not platform members, so names suffice.</summary>
    public IReadOnlyList<InterviewParticipantRequest> Participants { get; init; } = [];
}
