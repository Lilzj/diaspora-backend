namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One screening question as the editor sends it.
/// <para>
/// <see cref="QuestionId"/> is caller-supplied and may be empty for a new question. Preserving
/// it on an existing one is what keeps answers already given attached to the question they
/// answered when the employer reorders the list.
/// </para>
/// </summary>
public sealed record ScreeningQuestionDraft
{
    public Guid QuestionId { get; init; }

    public required string? Text { get; init; }

    public bool IsRequired { get; init; }
}
