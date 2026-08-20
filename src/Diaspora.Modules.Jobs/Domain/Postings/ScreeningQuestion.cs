using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// A question the employer asks every applicant.
/// <para>
/// <b>Deliberately free text with no answer type.</b> Multiple choice, numeric ranges and
/// yes/no gates are how screening questions become automated rejection, and automated rejection
/// on a platform whose members have non-linear careers and foreign qualifications rejects the
/// wrong people. A question a human reads and answers in their own words is slower and much
/// harder to weaponise. Automated interview scoring is explicitly out of scope for this phase,
/// and this is the shape that keeps it out.
/// </para>
/// <para>
/// The identity is caller-supplied so an answer can reference the question it answered, and
/// stays valid when the employer reorders the list.
/// </para>
/// </summary>
public sealed class ScreeningQuestion
{
    private ScreeningQuestion(Guid questionId, string text, bool isRequired, int order)
    {
        QuestionId = questionId;
        Text = text;
        IsRequired = isRequired;
        Order = order;
    }

    private ScreeningQuestion() { }

    /// <summary>Stable across reordering. What an answer points at.</summary>
    public Guid QuestionId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>Whether an application without an answer is refused.</summary>
    public bool IsRequired { get; private set; }

    public int Order { get; private set; }

    internal static ScreeningQuestion? Create(
        Guid questionId, string? text, bool isRequired, int order)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var collapsed = string.Join(' ', text.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (collapsed.Length == 0 || collapsed.Length > JobLimits.MaxQuestionLength)
        {
            return null;
        }

        return new ScreeningQuestion(
            questionId == Guid.Empty ? Guid.NewGuid() : questionId, collapsed, isRequired, order);
    }
}
