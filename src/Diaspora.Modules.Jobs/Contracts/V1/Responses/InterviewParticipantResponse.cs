namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One interviewer. Names rather than accounts, because a panel routinely includes people who
/// are not platform members — and the candidate needs to know who they are meeting either way.
/// </summary>
public sealed record InterviewParticipantResponse
{
    public required string Name { get; init; }

    public string? Role { get; init; }
}
