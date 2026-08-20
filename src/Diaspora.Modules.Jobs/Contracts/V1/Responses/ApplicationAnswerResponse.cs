namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One answer, with the question as it stood when it was answered.
/// <para>
/// The question text is the snapshot stored on the application, not the posting's current
/// wording. An employer who edits a question after applications arrive must not silently rewrite
/// what previous candidates appear to have been asked.
/// </para>
/// </summary>
public sealed record ApplicationAnswerResponse
{
    public required Guid QuestionId { get; init; }

    public required string QuestionText { get; init; }

    public required string Answer { get; init; }
}
