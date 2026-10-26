using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Academy;

/// <summary>
/// One creator's progress through one course.
/// <para>
/// <b>Server-side, because the register says local progress is not progress:</b> "progress kept only
/// on the device is lost on reinstall." This row is the answer to "where was I" on a second phone,
/// after a reinstall, and on the web.
/// </para>
/// <para>
/// <b>Each completed lesson records the content version it was completed at.</b> That is
/// CREATOR-GAP-006's versioning rule made concrete: a lesson rewritten from version 3 to version 4
/// leaves the member's completion standing — they did read it — and the read serves both numbers so
/// the app can offer "this has been updated" rather than the platform deciding for them.
/// </para>
/// <para>
/// <b>A quiz lesson is completed by <see cref="RecordQuizPass"/> and by nothing else.</b>
/// <see cref="CompleteLesson"/> refuses one outright, which is <c>creator.lesson.quiz_required</c>.
/// </para>
/// </summary>
public sealed class CreatorLearningProgress
    : AggregateRoot<CreatorLearningProgressId>, IAuditableEntity
{
    private readonly List<CompletedLesson> _completedLessons = [];
    private readonly List<QuizAttempt> _attempts = [];

    private CreatorLearningProgress(
        CreatorLearningProgressId id,
        CreatorId creatorId,
        Guid userId,
        AcademyCourseId courseId,
        string courseSlug,
        DateTimeOffset startedAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        CourseId = courseId;
        CourseSlug = courseSlug;
        State = CourseProgressState.NotStarted;
        CreatedAt = startedAt;
    }

    private CreatorLearningProgress() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public AcademyCourseId CourseId { get; private set; } = default!;

    public string CourseSlug { get; private set; } = string.Empty;

    public CourseProgressState State { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Whether the member saved the course. A flag, not a second aggregate.</summary>
    public bool IsSaved { get; private set; }

    public CreatorCertificateId? CertificateId { get; private set; }

    /// <summary>
    /// The reward reference the course completion published, and its anti-double-award barrier.
    /// </summary>
    public string? CompletionGrantReference { get; private set; }

    public IReadOnlyList<CompletedLesson> CompletedLessons => _completedLessons.AsReadOnly();

    public IReadOnlyList<QuizAttempt> Attempts => _attempts.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorLearningProgress Begin(
        CreatorLearningProgressId id,
        CreatorId creatorId,
        Guid userId,
        AcademyCourse course,
        DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(course);

        return new CreatorLearningProgress(
            id, creatorId, userId, course.Id, course.Slug, startedAt);
    }

    /// <summary>
    /// Marks a lesson read.
    /// <para>
    /// <b>Idempotent, and refused for a quiz lesson.</b> A second completion of the same lesson at
    /// the same content version is a no-op rather than an error — a retried request is an ordinary
    /// event, and answering it with a 409 would make an offline client's queue unplayable. A
    /// completion at a <em>newer</em> version updates the recorded version, because the member has
    /// now read the new one.
    /// </para>
    /// </summary>
    public Result CompleteLesson(AcademyLesson lesson, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lesson);

        if (!lesson.CanBeCompletedByAssertion)
        {
            return Result.Failure(CreatorErrors.LessonQuizRequired);
        }

        MarkComplete(lesson.Id, lesson.ContentVersion, now);

        return Result.Success();
    }

    /// <summary>
    /// Records a quiz attempt, and completes the lesson when it passed.
    /// <para>
    /// The attempt is recorded whichever way it went, because the retry cooldown is computed from
    /// it and a failed attempt nobody stored would let a member brute-force a four-option quiz.
    /// </para>
    /// </summary>
    public void RecordQuizPass(
        AcademyLesson lesson, AcademyQuizId quizId, bool passed, int score, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lesson);

        var attempt = _attempts.Find(candidate => candidate.QuizId == quizId);

        if (attempt is null)
        {
            attempt = QuizAttempt.Begin(quizId);
            _attempts.Add(attempt);
        }

        attempt.Record(passed, score, now);

        if (passed)
        {
            MarkComplete(lesson.Id, lesson.ContentVersion, now);
        }
        else
        {
            Touch(now);
        }
    }

    /// <summary>Whether the member may try this quiz again, and when.</summary>
    public (bool CanRetry, DateTimeOffset? AvailableAt) RetryWindow(
        AcademyQuizId quizId, DateTimeOffset now)
    {
        var attempt = _attempts.Find(candidate => candidate.QuizId == quizId);

        if (attempt is null || attempt.Passed)
        {
            return (true, null);
        }

        var available = attempt.LastAttemptAt.AddMinutes(CreatorLimits.QuizRetryCooldownMinutes);

        return available <= now ? (true, null) : (false, available);
    }

    /// <summary>
    /// Re-derives the course state from the lessons completed.
    /// <para>
    /// Returns true on the transition into <see cref="CourseProgressState.Completed"/>, which is
    /// what makes the certificate and the completion points a once-per-course consequence.
    /// </para>
    /// </summary>
    public bool Recompute(IReadOnlyList<AcademyLessonId> courseLessons, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(courseLessons);

        var previous = State;

        if (_completedLessons.Count == 0)
        {
            State = CourseProgressState.NotStarted;
            Touch(now);
            return false;
        }

        var complete = courseLessons.Count > 0
            && courseLessons.All(lessonId =>
                _completedLessons.Exists(done => done.LessonId == lessonId));

        State = complete ? CourseProgressState.Completed : CourseProgressState.InProgress;

        if (complete && previous != CourseProgressState.Completed)
        {
            CompletedAt = now;
            Touch(now);
            return true;
        }

        Touch(now);

        return false;
    }

    /// <summary>
    /// The course finished, so the certificate follows and the completion is announced.
    /// <para>
    /// Separate from <see cref="Recompute"/> so the reference that marks the completion done is
    /// supplied by the handler that owns idempotency rather than invented inside the aggregate.
    /// </para>
    /// <para>
    /// <b>Pays no points (Q6).</b> It paid the course's <see cref="AcademyCourse.CompletionPoints"/>
    /// until 2026-10-25; points are now paid only for confirmed outcomes (D-2). A completion earns
    /// XP, from Trust off <see cref="CourseCompletedDomainEvent"/>, and counts toward the
    /// <c>academy-graduate</c> milestone, whose own points still pay. Points already earned stay.
    /// </para>
    /// </summary>
    public void AwardCompletion(
        AcademyCourse course,
        CreatorCertificateId? certificateId,
        string grantReference,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);

        if (CompletionGrantReference is not null)
        {
            return;
        }

        CertificateId = certificateId;
        CompletionGrantReference = grantReference;
        Touch(now);

        RaiseDomainEvent(new CourseCompletedDomainEvent(CreatorId, UserId, CourseId, course.Slug, now));
    }

    public void SetSaved(bool isSaved, DateTimeOffset now)
    {
        IsSaved = isSaved;
        Touch(now);
    }

    /// <summary>Where "Continue" goes: the first lesson of the course not yet completed.</summary>
    public AcademyLessonId? NextLesson(IReadOnlyList<AcademyLessonId> courseLessons)
    {
        ArgumentNullException.ThrowIfNull(courseLessons);

        return courseLessons.FirstOrDefault(lessonId =>
            !_completedLessons.Exists(done => done.LessonId == lessonId));
    }

    public bool HasCompleted(AcademyLessonId lessonId) =>
        _completedLessons.Exists(done => done.LessonId == lessonId);

    /// <summary>The version a lesson was completed at, or null when it has not been.</summary>
    public int? CompletedAtVersion(AcademyLessonId lessonId) =>
        _completedLessons.Find(done => done.LessonId == lessonId)?.ContentVersion;

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

    private void MarkComplete(AcademyLessonId lessonId, int contentVersion, DateTimeOffset now)
    {
        StartedAt ??= now;

        var existing = _completedLessons.Find(done => done.LessonId == lessonId);

        if (existing is null)
        {
            _completedLessons.Add(CompletedLesson.Create(lessonId, contentVersion, now));
        }
        else
        {
            existing.Refresh(contentVersion, now);
        }

        if (State == CourseProgressState.NotStarted)
        {
            State = CourseProgressState.InProgress;
        }

        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}

/// <summary>
/// A lesson the member has finished, and the content version they finished.
/// <para>
/// The version is the whole reason this is a row rather than a set of identifiers.
/// </para>
/// </summary>
public sealed class CompletedLesson
{
    private CompletedLesson(AcademyLessonId lessonId, int contentVersion, DateTimeOffset completedAt)
    {
        LessonId = lessonId;
        ContentVersion = contentVersion;
        CompletedAt = completedAt;
    }

    private CompletedLesson() { }

    public AcademyLessonId LessonId { get; private set; } = default!;

    /// <summary>The lesson's content version at the moment it was completed.</summary>
    public int ContentVersion { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public static CompletedLesson Create(
        AcademyLessonId lessonId, int contentVersion, DateTimeOffset completedAt) =>
        new(lessonId, contentVersion, completedAt);

    internal void Refresh(int contentVersion, DateTimeOffset completedAt)
    {
        if (contentVersion <= ContentVersion)
        {
            return;
        }

        ContentVersion = contentVersion;
        CompletedAt = completedAt;
    }
}

/// <summary>What has happened on one quiz. The retry cooldown's input.</summary>
public sealed class QuizAttempt
{
    private QuizAttempt(AcademyQuizId quizId) => QuizId = quizId;

    private QuizAttempt() { }

    public AcademyQuizId QuizId { get; private set; } = default!;

    public int AttemptCount { get; private set; }

    public bool Passed { get; private set; }

    public int LastScore { get; private set; }

    public DateTimeOffset LastAttemptAt { get; private set; }

    public static QuizAttempt Begin(AcademyQuizId quizId) => new(quizId);

    internal void Record(bool passed, int score, DateTimeOffset now)
    {
        AttemptCount++;
        Passed = Passed || passed;
        LastScore = score;
        LastAttemptAt = now;
    }
}

/// <summary>
/// A certificate.
/// <para>
/// <b>Issued by the server against a persisted completion, and by nothing else.</b> §32 of the phase
/// brief: "Certificates must correspond to actual persisted completion." The issuing handler reads
/// the progress row and refuses without it; there is no route that mints one.
/// </para>
/// </summary>
public sealed class CreatorCertificate : AggregateRoot<CreatorCertificateId>, IAuditableEntity
{
    private CreatorCertificate(
        CreatorCertificateId id,
        CreatorId creatorId,
        Guid userId,
        AcademyCourseId courseId,
        string courseTitle,
        string recipientName,
        string verificationReference,
        DateTimeOffset issuedAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        CourseId = courseId;
        CourseTitle = courseTitle;
        RecipientName = recipientName;
        VerificationReference = verificationReference;
        IssuedAt = issuedAt;
        CreatedAt = issuedAt;
    }

    private CreatorCertificate() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public AcademyCourseId CourseId { get; private set; } = default!;

    public string CourseTitle { get; private set; } = string.Empty;

    /// <summary>The name printed on it, as the platform holds it.</summary>
    public string RecipientName { get; private set; } = string.Empty;

    /// <summary>A server-issued reference somebody else can check. Uniquely indexed.</summary>
    public string VerificationReference { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorCertificate Issue(
        CreatorCertificateId id,
        CreatorId creatorId,
        Guid userId,
        AcademyCourse course,
        string recipientName,
        string verificationReference,
        DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(course);

        return new CreatorCertificate(
            id,
            creatorId,
            userId,
            course.Id,
            course.Title,
            string.IsNullOrWhiteSpace(recipientName) ? "Member" : recipientName.Trim(),
            verificationReference,
            issuedAt);
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
