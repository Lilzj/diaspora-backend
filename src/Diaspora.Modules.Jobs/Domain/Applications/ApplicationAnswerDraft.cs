namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// One answer as the application form sends it. The question text is filled in by the handler
/// from the posting, not trusted from the client — a candidate must not be able to choose what
/// they appear to have been asked.
/// </summary>
public sealed record ApplicationAnswerDraft
{
    public required Guid QuestionId { get; init; }

    public required string? Answer { get; init; }
}
