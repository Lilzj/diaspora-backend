namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A question the employer asks every applicant.
/// <para>
/// Free text with no answer type, deliberately. Multiple choice and yes/no gates are how
/// screening questions become automated rejection, and automated rejection on a platform whose
/// members have non-linear careers and foreign qualifications rejects the wrong people.
/// </para>
/// </summary>
public sealed record JobScreeningQuestionResponse
{
    public required Guid QuestionId { get; init; }

    public required string Text { get; init; }

    public required bool IsRequired { get; init; }
}
