using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// One answer to one of the posting's screening questions.
/// <para>
/// <b>The question text is copied here, not referenced.</b> That is deliberate duplication and
/// the one place in this module where copying is right: an employer who edits a question after
/// applications arrive would otherwise silently rewrite what every previous candidate appears to
/// have been asked. The snapshot is what makes the answer mean anything a year later.
/// </para>
/// </summary>
public sealed class ApplicationAnswer
{
    private ApplicationAnswer(Guid questionId, string questionText, string answer, int order)
    {
        QuestionId = questionId;
        QuestionText = questionText;
        Answer = answer;
        Order = order;
    }

    private ApplicationAnswer() { }

    /// <summary>Which question this answers. Stable across the employer reordering them.</summary>
    public Guid QuestionId { get; private set; }

    /// <summary>The question as it stood when this candidate answered it. A snapshot.</summary>
    public string QuestionText { get; private set; } = string.Empty;

    public string Answer { get; private set; } = string.Empty;

    public int Order { get; private set; }

    internal static ApplicationAnswer? Create(
        Guid questionId, string? questionText, string? answer, int order)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var text = answer.Trim();

        if (text.Length > JobLimits.MaxAnswerLength)
        {
            return null;
        }

        var question = string.IsNullOrWhiteSpace(questionText)
            ? string.Empty
            : questionText.Trim();

        if (question.Length > JobLimits.MaxQuestionLength)
        {
            question = question[..JobLimits.MaxQuestionLength];
        }

        return new ApplicationAnswer(questionId, question, text, order);
    }
}
