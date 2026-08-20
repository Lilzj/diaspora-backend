using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Academy;

/// <summary>
/// A quiz, and the only place the answer key exists.
/// <para>
/// <b>The key never leaves this aggregate.</b> The register: "<c>LessonQuizOption</c> carries no
/// <c>isCorrect</c>. The answer key never reaches the device." That is true here twice over — the
/// quiz read projects <see cref="AcademyQuizOption.Label"/> and nothing else, and
/// <see cref="Grade"/> is the only member that reads <see cref="AcademyQuizOption.IsCorrect"/>. A
/// projection that forgot to exclude the flag would have to be written inside this file to be able
/// to leak it, which is the point of putting the grading here rather than in a handler.
/// </para>
/// <para>
/// <b>Questions and options are two flat owned collections rather than one nested pair</b>, for the
/// reason <c>AcademyLesson.BlockItems</c> gives: a nested owned collection is a shape this platform
/// has never shipped, and a JSON column is one §27 forbids.
/// </para>
/// </summary>
public sealed class AcademyQuiz : AggregateRoot<AcademyQuizId>, IAuditableEntity
{
    private readonly List<AcademyQuizQuestion> _questions = [];
    private readonly List<AcademyQuizOption> _options = [];

    private AcademyQuiz(AcademyQuizId id, AcademyLessonId lessonId, string key, int passMark)
        : base(id)
    {
        LessonId = lessonId;
        Key = key;
        PassMark = passMark;
    }

    private AcademyQuiz() { }

    public AcademyLessonId LessonId { get; private set; } = default!;

    /// <summary>Stable and unique. What a seeder updates a quiz by.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>How many questions must be right. Served — the member is told the bar.</summary>
    public int PassMark { get; private set; }

    public int ContentVersion { get; private set; } = 1;

    public IReadOnlyList<AcademyQuizQuestion> Questions => _questions.AsReadOnly();

    /// <summary>Every option across every question, keyed back by <c>QuestionId</c>.</summary>
    public IReadOnlyList<AcademyQuizOption> Options => _options.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static AcademyQuiz Define(
        AcademyQuizId id, AcademyLessonId lessonId, string key, int passMark) =>
        new(id, lessonId, key.Trim(), Math.Max(1, passMark));

    /// <summary>Adds a question and its options. Exactly one option is the answer.</summary>
    public AcademyQuiz WithQuestion(
        AcademyQuizQuestionId questionId,
        string prompt,
        string? explanation,
        IEnumerable<(Guid OptionId, string Label, bool IsCorrect)> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_questions.Count >= CreatorLimits.MaxQuizQuestions)
        {
            return this;
        }

        _questions.Add(AcademyQuizQuestion.Create(
            questionId, prompt, explanation, _questions.Count));

        var order = 0;

        foreach (var (optionId, label, isCorrect) in options.Take(CreatorLimits.MaxQuizOptions))
        {
            _options.Add(AcademyQuizOption.Create(optionId, questionId, label, isCorrect, order));
            order++;
        }

        return this;
    }

    public void Redefine(
        int passMark,
        IEnumerable<AcademyQuizQuestion> questions,
        IEnumerable<AcademyQuizOption> options)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(options);

        PassMark = Math.Max(1, passMark);
        ContentVersion++;

        _questions.Clear();
        _questions.AddRange(questions);

        _options.Clear();
        _options.AddRange(options);
    }

    /// <summary>The options belonging to one question, in order.</summary>
    public IReadOnlyList<AcademyQuizOption> OptionsFor(AcademyQuizQuestionId questionId) =>
        _options
            .Where(option => option.QuestionId == questionId)
            .OrderBy(option => option.SortOrder)
            .ToList();

    /// <summary>
    /// Grades a submission.
    /// <para>
    /// <b>The only member that reads the answer key, and it returns an outcome rather than the
    /// key.</b> A question the member did not answer is wrong rather than skipped: a quiz that let
    /// somebody pass by answering only the ones they knew would have a pass mark that meant
    /// nothing.
    /// </para>
    /// <para>
    /// An answer naming an option that is not on the question is refused outright rather than
    /// scored zero — that is a malformed request, not a wrong answer, and telling the difference is
    /// how a client bug stays visible.
    /// </para>
    /// </summary>
    public QuizGrading Grade(IReadOnlyDictionary<Guid, Guid> answersByQuestion)
    {
        ArgumentNullException.ThrowIfNull(answersByQuestion);

        var outcomes = new List<QuizQuestionOutcome>(_questions.Count);
        var correct = 0;

        foreach (var question in _questions.OrderBy(question => question.SortOrder))
        {
            var options = OptionsFor(question.QuestionId);

            if (!answersByQuestion.TryGetValue(question.QuestionId.Value, out var chosen))
            {
                outcomes.Add(new QuizQuestionOutcome(
                    question.QuestionId.Value, false, question.Explanation));

                continue;
            }

            if (!options.Any(option => option.OptionId == chosen))
            {
                return QuizGrading.Malformed();
            }

            var isCorrect = options.Any(option => option.OptionId == chosen && option.IsCorrect);

            if (isCorrect)
            {
                correct++;
            }

            outcomes.Add(new QuizQuestionOutcome(
                question.QuestionId.Value, isCorrect, question.Explanation));
        }

        return QuizGrading.Graded(correct >= PassMark, correct, _questions.Count, outcomes);
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}

/// <summary>One question. Carries no answer.</summary>
public sealed class AcademyQuizQuestion : ValueObject
{
    private AcademyQuizQuestion(
        AcademyQuizQuestionId questionId, string prompt, string? explanation, int sortOrder)
    {
        QuestionId = questionId;
        Prompt = prompt;
        Explanation = explanation;
        SortOrder = sortOrder;
    }

    private AcademyQuizQuestion() { }

    public AcademyQuizQuestionId QuestionId { get; private set; } = default!;

    public string Prompt { get; private set; } = string.Empty;

    /// <summary>Shown after answering, whichever way it went.</summary>
    public string? Explanation { get; private set; }

    public int SortOrder { get; private set; }

    public static AcademyQuizQuestion Create(
        AcademyQuizQuestionId questionId, string prompt, string? explanation, int sortOrder) =>
        new(questionId, prompt.Trim(), explanation?.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return QuestionId;
    }
}

/// <summary>
/// One option on one question.
/// <para>
/// <b><see cref="IsCorrect"/> is the answer key.</b> It is stored, it is never projected, and the
/// only code that reads it is <see cref="AcademyQuiz.Grade"/>. An integration test posts the quiz
/// read and asserts the flag is nowhere in the response body.
/// </para>
/// </summary>
public sealed class AcademyQuizOption : ValueObject
{
    private AcademyQuizOption(
        Guid optionId,
        AcademyQuizQuestionId questionId,
        string label,
        bool isCorrect,
        int sortOrder)
    {
        OptionId = optionId;
        QuestionId = questionId;
        Label = label;
        IsCorrect = isCorrect;
        SortOrder = sortOrder;
    }

    private AcademyQuizOption() { }

    /// <summary>Caller-supplied, so <c>ValueGeneratedNever()</c>. The client names it in an answer.</summary>
    public Guid OptionId { get; private set; }

    public AcademyQuizQuestionId QuestionId { get; private set; } = default!;

    public string Label { get; private set; } = string.Empty;

    /// <summary>Never leaves this aggregate. See the class summary.</summary>
    public bool IsCorrect { get; private set; }

    public int SortOrder { get; private set; }

    public static AcademyQuizOption Create(
        Guid optionId,
        AcademyQuizQuestionId questionId,
        string label,
        bool isCorrect,
        int sortOrder) =>
        new(optionId, questionId, label.Trim(), isCorrect, sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OptionId;
    }
}

/// <summary>What grading produced. Never the key.</summary>
public sealed record QuizGrading
{
    private QuizGrading(
        bool isMalformed,
        bool passed,
        int correctCount,
        int totalCount,
        IReadOnlyList<QuizQuestionOutcome> outcomes)
    {
        IsMalformed = isMalformed;
        Passed = passed;
        CorrectCount = correctCount;
        TotalCount = totalCount;
        Outcomes = outcomes;
    }

    /// <summary>An answer named an option that is not on the question. A client bug, not a wrong answer.</summary>
    public bool IsMalformed { get; }

    public bool Passed { get; }

    public int CorrectCount { get; }

    public int TotalCount { get; }

    public IReadOnlyList<QuizQuestionOutcome> Outcomes { get; }

    public static QuizGrading Malformed() => new(true, false, 0, 0, []);

    public static QuizGrading Graded(
        bool passed, int correctCount, int totalCount, IReadOnlyList<QuizQuestionOutcome> outcomes) =>
        new(false, passed, correctCount, totalCount, outcomes);
}

/// <summary>One question's outcome, with the explanation the member may now see.</summary>
public sealed record QuizQuestionOutcome(Guid QuestionId, bool IsCorrect, string? Explanation);
