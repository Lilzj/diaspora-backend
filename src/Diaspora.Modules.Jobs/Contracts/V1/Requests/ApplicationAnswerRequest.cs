namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// One answer to a screening question.
/// <para>
/// Only the question's identifier is sent. The question <em>text</em> is copied onto the
/// application by the handler, from the posting — a candidate must not be able to choose what
/// they appear to have been asked.
/// </para>
/// </summary>
public sealed record ApplicationAnswerRequest
{
    public required Guid QuestionId { get; init; }

    public required string Answer { get; init; }
}
