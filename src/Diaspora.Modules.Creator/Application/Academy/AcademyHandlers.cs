using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Academy;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-006 — the Creator Academy.
//
// Seven routes. The register's two constraints are load-bearing and both are structural:
//
//   Lessons are blocks, never markup. There is no column in the schema that could hold a script.
//
//   The answer key never reaches the device. The quiz projection has no isCorrect on it — see
//   CreatorMapper — and POST /lessons/{id}/complete on a quiz lesson is refused with
//   creator.lesson.quiz_required, which is the client's own error code.
//
// Progress is versioned: a lesson rewritten from version 3 to 4 leaves a completion standing and the
// read says the lesson has been revised, rather than the platform deciding for the member.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The Academy's sections, with the courses in each.</summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record ListAcademySectionsQuery : IQuery<IReadOnlyList<AcademySectionResponse>>;

public sealed class ListAcademySectionsQueryHandler
    : IQueryHandler<ListAcademySectionsQuery, IReadOnlyList<AcademySectionResponse>>
{
    private readonly IAcademyRepository _academy;

    public ListAcademySectionsQueryHandler(IAcademyRepository academy) => _academy = academy;

    public async Task<Result<IReadOnlyList<AcademySectionResponse>>> Handle(
        ListAcademySectionsQuery query, CancellationToken cancellationToken)
    {
        var sections = await _academy.ListSectionsAsync(cancellationToken);
        var courses = await _academy.ListCoursesAsync(null, null, cancellationToken);

        var response = sections
            .Select(section => new AcademySectionResponse
            {
                Key = section.Key,
                Title = section.Title,
                Description = section.Description,
                Icon = section.Icon,
                CourseIds = courses
                    .Where(course => string.Equals(
                        course.SectionKey, section.Key, StringComparison.Ordinal))
                    .OrderBy(course => course.SortOrder)
                    .Select(course => course.Id.Value)
                    .ToList(),
                SortOrder = section.SortOrder
            })
            .ToList();

        return Result.Success<IReadOnlyList<AcademySectionResponse>>(response);
    }
}

/// <summary>The course catalogue, with this member's progress against each.</summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record ListAcademyCoursesQuery : IQuery<IReadOnlyList<CreatorCourseResponse>>
{
    public string? Section { get; init; }

    public string? Level { get; init; }
}

public sealed class ListAcademyCoursesQueryHandler
    : IQueryHandler<ListAcademyCoursesQuery, IReadOnlyList<CreatorCourseResponse>>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly ICreatorLearningProgressRepository _progress;

    public ListAcademyCoursesQueryHandler(
        CreatorContext context,
        IAcademyRepository academy,
        ICreatorLearningProgressRepository progress)
    {
        _context = context;
        _academy = academy;
        _progress = progress;
    }

    public async Task<Result<IReadOnlyList<CreatorCourseResponse>>> Handle(
        ListAcademyCoursesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorCourseResponse>>(caller.Error);
        }

        var level = Enum.TryParse<CourseLevel>(query.Level, ignoreCase: true, out var parsed)
            ? parsed
            : (CourseLevel?)null;

        var courses = await _academy.ListCoursesAsync(query.Section, level, cancellationToken);
        var creator = await _context.FindCreatorAsync(cancellationToken);

        var rows = creator is null
            ? []
            : (await _progress.ListForCreatorAsync(creator.Id, cancellationToken))
                .ToDictionary(row => row.CourseId);

        var response = new List<CreatorCourseResponse>(courses.Count);

        foreach (var course in courses)
        {
            var lessons = (await _academy.ListLessonsAsync(course.Id, cancellationToken))
                .Select(lesson => lesson.Id)
                .ToList();

            rows.TryGetValue(course.Id, out var row);

            response.Add(course.ToResponse(row, lessons));
        }

        return Result.Success<IReadOnlyList<CreatorCourseResponse>>(response);
    }
}

/// <summary>
/// A course's lessons.
/// <para>
/// The quiz on a quiz lesson is served without its answers — see <c>CreatorMapper.ToResponse</c>,
/// which has no overload that could include one.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record ListCourseLessonsQuery : IQuery<IReadOnlyList<CreatorLessonResponse>>
{
    public required Guid CourseId { get; init; }
}

public sealed class ListCourseLessonsQueryHandler
    : IQueryHandler<ListCourseLessonsQuery, IReadOnlyList<CreatorLessonResponse>>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly ICreatorLearningProgressRepository _progress;

    public ListCourseLessonsQueryHandler(
        CreatorContext context,
        IAcademyRepository academy,
        ICreatorLearningProgressRepository progress)
    {
        _context = context;
        _academy = academy;
        _progress = progress;
    }

    public async Task<Result<IReadOnlyList<CreatorLessonResponse>>> Handle(
        ListCourseLessonsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorLessonResponse>>(caller.Error);
        }

        var course = await _academy.GetCourseAsync(
            AcademyCourseId.From(query.CourseId), cancellationToken);

        if (course is null || !course.IsPublished)
        {
            return Result.Failure<IReadOnlyList<CreatorLessonResponse>>(CreatorErrors.CourseNotFound);
        }

        var lessons = await _academy.ListLessonsAsync(course.Id, cancellationToken);
        var creator = await _context.FindCreatorAsync(cancellationToken);

        var row = creator is null
            ? null
            : await _progress.FindAsync(creator.Id, course.Id, cancellationToken);

        var response = new List<CreatorLessonResponse>(lessons.Count);

        foreach (var lesson in lessons)
        {
            var quiz = lesson.QuizId is { } quizId
                ? await _academy.GetQuizAsync(quizId, cancellationToken)
                : null;

            response.Add(lesson.ToResponse(quiz, row));
        }

        return Result.Success<IReadOnlyList<CreatorLessonResponse>>(response);
    }
}

/// <summary>
/// One course, by id.
/// <para>
/// <b>Added 21 Oct 2026 for the app's course screen, which deep-links by id.</b> The access rule is
/// the catalogue's: any member who may read the Academy, and only a published course. An
/// unpublished course is the same 404 a missing one is, as <see cref="ListCourseLessonsQuery"/>
/// already answers. The Academy has no paid or entitlement-gated course; if one is ever added, this
/// read and the catalogue must gain the gate together.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record GetAcademyCourseQuery : IQuery<CreatorCourseResponse>
{
    public required Guid CourseId { get; init; }
}

public sealed class GetAcademyCourseQueryHandler
    : IQueryHandler<GetAcademyCourseQuery, CreatorCourseResponse>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly ICreatorLearningProgressRepository _progress;

    public GetAcademyCourseQueryHandler(
        CreatorContext context,
        IAcademyRepository academy,
        ICreatorLearningProgressRepository progress)
    {
        _context = context;
        _academy = academy;
        _progress = progress;
    }

    public async Task<Result<CreatorCourseResponse>> Handle(
        GetAcademyCourseQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorCourseResponse>(caller.Error);
        }

        var course = await _academy.GetCourseAsync(
            AcademyCourseId.From(query.CourseId), cancellationToken);

        if (course is null || !course.IsPublished)
        {
            return Result.Failure<CreatorCourseResponse>(CreatorErrors.CourseNotFound);
        }

        var lessons = (await _academy.ListLessonsAsync(course.Id, cancellationToken))
            .Select(lesson => lesson.Id)
            .ToList();

        var creator = await _context.FindCreatorAsync(cancellationToken);

        var row = creator is null
            ? null
            : await _progress.FindAsync(creator.Id, course.Id, cancellationToken);

        return Result.Success(course.ToResponse(row, lessons));
    }
}

/// <summary>
/// One lesson, by id — the app's legacy lesson deep links.
/// <para>
/// <b>Served exactly as the course's lesson list serves it</b>: the same projection, so a quiz comes
/// without its answers (<c>CreatorMapper</c> has no overload that could include them), and the same
/// visibility — the lesson <em>and</em> its course must both be published. Checking only the lesson
/// would let a published lesson inside a withdrawn course be read by id after the course had gone
/// from the catalogue.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record GetAcademyLessonQuery : IQuery<CreatorLessonResponse>
{
    public required Guid LessonId { get; init; }
}

public sealed class GetAcademyLessonQueryHandler
    : IQueryHandler<GetAcademyLessonQuery, CreatorLessonResponse>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly ICreatorLearningProgressRepository _progress;

    public GetAcademyLessonQueryHandler(
        CreatorContext context,
        IAcademyRepository academy,
        ICreatorLearningProgressRepository progress)
    {
        _context = context;
        _academy = academy;
        _progress = progress;
    }

    public async Task<Result<CreatorLessonResponse>> Handle(
        GetAcademyLessonQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorLessonResponse>(caller.Error);
        }

        var lesson = await _academy.GetLessonAsync(
            AcademyLessonId.From(query.LessonId), cancellationToken);

        if (lesson is null || !lesson.IsPublished)
        {
            return Result.Failure<CreatorLessonResponse>(CreatorErrors.LessonNotFound);
        }

        var course = await _academy.GetCourseAsync(lesson.CourseId, cancellationToken);

        if (course is null || !course.IsPublished)
        {
            // The lesson's own error, not the course's: the caller asked for a lesson, and naming
            // the course would confirm that an unpublished one exists.
            return Result.Failure<CreatorLessonResponse>(CreatorErrors.LessonNotFound);
        }

        var quiz = lesson.QuizId is { } quizId
            ? await _academy.GetQuizAsync(quizId, cancellationToken)
            : null;

        var creator = await _context.FindCreatorAsync(cancellationToken);

        var row = creator is null
            ? null
            : await _progress.FindAsync(creator.Id, course.Id, cancellationToken);

        return Result.Success(lesson.ToResponse(quiz, row));
    }
}

/// <summary>
/// Marks a lesson read.
/// <para>
/// <b>Refused for a quiz lesson with the client's own error code.</b> The register: "<c>POST
/// /lessons/{id}/complete</c> on a quiz lesson is refused with <c>creator.lesson.quiz_required</c>."
/// That refusal is what stops "client → mark complete → certificate", which §32 of the phase brief
/// names as the failure to prevent.
/// </para>
/// <para>
/// <b>Idempotent by construction rather than by a key.</b> Completing the same lesson twice at the
/// same content version is a no-op — an offline client's queue must be replayable — and completing
/// it at a newer version updates the recorded version, because the member has now read the new one.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.Participate)]
[Transactional(Reason =
    "Writes the lesson completion and, when it finishes the course, the certificate. A completion "
    + "that committed without its certificate would leave a finished course nobody was credited for.")]
[Auditable(Action = "creator.lesson.complete")]
public sealed record CompleteLessonCommand : ICommand<CreatorCourseProgressResponse>
{
    public required Guid LessonId { get; init; }

    public CompleteLessonRequest? Request { get; init; }
}

public sealed class CompleteLessonCommandValidator : AbstractValidator<CompleteLessonCommand>
{
    public CompleteLessonCommandValidator() => RuleFor(command => command.LessonId).NotEmpty();
}

public sealed class CompleteLessonCommandHandler
    : ICommandHandler<CompleteLessonCommand, CreatorCourseProgressResponse>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly CourseCompletionService _completion;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteLessonCommandHandler(
        CreatorContext context,
        IAcademyRepository academy,
        CourseCompletionService completion,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _academy = academy;
        _completion = completion;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorCourseProgressResponse>> Handle(
        CompleteLessonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorCourseProgressResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorCourseProgressResponse>(allowed.Error);
        }

        var lesson = await _academy.GetLessonAsync(
            AcademyLessonId.From(command.LessonId), cancellationToken);

        if (lesson is null || !lesson.IsPublished)
        {
            return Result.Failure<CreatorCourseProgressResponse>(CreatorErrors.LessonNotFound);
        }

        var course = await _academy.GetCourseAsync(lesson.CourseId, cancellationToken);

        if (course is null)
        {
            return Result.Failure<CreatorCourseProgressResponse>(CreatorErrors.CourseNotFound);
        }

        var progress = await _completion.OpenAsync(creator.Value, course, cancellationToken);

        var completed = progress.CompleteLesson(lesson, _clock.UtcNow);

        if (completed.IsFailure)
        {
            return Result.Failure<CreatorCourseProgressResponse>(completed.Error);
        }

        var rendered = await _completion.SettleAsync(
            creator.Value, course, progress, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(rendered);
    }
}

/// <summary>
/// Grades a quiz submission.
/// <para>
/// <b>The only place the answer key is read, and the result is what comes back.</b> A submission
/// naming an option that is not on the question is a malformed request rather than a wrong answer,
/// and is refused as such — telling the two apart is how a client bug stays visible.
/// </para>
/// <para>
/// <b>A failed attempt is recorded.</b> The retry cooldown is computed from it, which is what stops
/// a member brute-forcing a four-option quiz.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.Participate)]
[Transactional(Reason =
    "Records the attempt, completes the lesson when it passed, and issues the certificate when that "
    + "finishes the course. Three writes that must land together or not at all.")]
[Auditable(Action = "creator.quiz.submit")]
public sealed record SubmitQuizCommand : ICommand<LessonQuizResultResponse>
{
    public required Guid QuizId { get; init; }

    public required SubmitQuizRequest Request { get; init; }
}

public sealed class SubmitQuizCommandValidator : AbstractValidator<SubmitQuizCommand>
{
    public SubmitQuizCommandValidator()
    {
        RuleFor(command => command.QuizId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Answers).NotNull();
    }
}

public sealed class SubmitQuizCommandHandler
    : ICommandHandler<SubmitQuizCommand, LessonQuizResultResponse>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly CourseCompletionService _completion;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SubmitQuizCommandHandler(
        CreatorContext context,
        IAcademyRepository academy,
        CourseCompletionService completion,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _academy = academy;
        _completion = completion;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<LessonQuizResultResponse>> Handle(
        SubmitQuizCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<LessonQuizResultResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<LessonQuizResultResponse>(allowed.Error);
        }

        var quiz = await _academy.GetQuizAsync(
            AcademyQuizId.From(command.QuizId), cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<LessonQuizResultResponse>(CreatorErrors.QuizNotFound);
        }

        var lesson = await _academy.GetLessonAsync(quiz.LessonId, cancellationToken);

        if (lesson is null)
        {
            return Result.Failure<LessonQuizResultResponse>(CreatorErrors.LessonNotFound);
        }

        var course = await _academy.GetCourseAsync(lesson.CourseId, cancellationToken);

        if (course is null)
        {
            return Result.Failure<LessonQuizResultResponse>(CreatorErrors.CourseNotFound);
        }

        var progress = await _completion.OpenAsync(creator.Value, course, cancellationToken);

        var now = _clock.UtcNow;
        var (canRetry, availableAt) = progress.RetryWindow(quiz.Id, now);

        if (!canRetry)
        {
            return Result.Failure<LessonQuizResultResponse>(CreatorErrors.QuizRetryNotAvailable);
        }

        var grading = quiz.Grade(command.Request.Answers);

        if (grading.IsMalformed)
        {
            return Result.Failure<LessonQuizResultResponse>(CreatorErrors.QuizAnswerUnknown);
        }

        progress.RecordQuizPass(lesson, quiz.Id, grading.Passed, grading.CorrectCount, now);

        var rendered = await _completion.SettleAsync(
            creator.Value, course, progress, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (retry, retryAt) = progress.RetryWindow(quiz.Id, now);

        return Result.Success(new LessonQuizResultResponse
        {
            Passed = grading.Passed,
            CorrectCount = grading.CorrectCount,
            TotalCount = grading.TotalCount,
            Outcomes = grading.Outcomes
                .Select(outcome => new QuizOutcomeResponse(
                    outcome.QuestionId, outcome.IsCorrect, outcome.Explanation))
                .ToList(),
            CanRetry = !grading.Passed && retry,
            RetryAvailableAt = grading.Passed ? null : retryAt ?? availableAt,
            Progress = rendered
        });
    }
}

/// <summary>Saves or unsaves a course.</summary>
[RequirePermission(CreatorPermissions.Participate)]
public sealed record SaveCourseCommand : ICommand<CreatorCourseResponse>
{
    public required Guid CourseId { get; init; }

    public required SaveCourseRequest Request { get; init; }
}

public sealed class SaveCourseCommandHandler
    : ICommandHandler<SaveCourseCommand, CreatorCourseResponse>
{
    private readonly CreatorContext _context;
    private readonly IAcademyRepository _academy;
    private readonly CourseCompletionService _completion;
    private readonly ICreatorLearningProgressRepository _progress;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SaveCourseCommandHandler(
        CreatorContext context,
        IAcademyRepository academy,
        CourseCompletionService completion,
        ICreatorLearningProgressRepository progress,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _academy = academy;
        _completion = completion;
        _progress = progress;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorCourseResponse>> Handle(
        SaveCourseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorCourseResponse>(creator.Error);
        }

        var course = await _academy.GetCourseAsync(
            AcademyCourseId.From(command.CourseId), cancellationToken);

        if (course is null || !course.IsPublished)
        {
            return Result.Failure<CreatorCourseResponse>(CreatorErrors.CourseNotFound);
        }

        var row = await _completion.OpenAsync(creator.Value, course, cancellationToken);

        row.SetSaved(command.Request.IsSaved, _clock.UtcNow);

        _progress.Update(row);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var lessons = (await _academy.ListLessonsAsync(course.Id, cancellationToken))
            .Select(lesson => lesson.Id)
            .ToList();

        return Result.Success(course.ToResponse(row, lessons));
    }
}

/// <summary>The caller's certificates.</summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record ListMyCertificatesQuery : IQuery<IReadOnlyList<CreatorCertificateResponse>>;

public sealed class ListMyCertificatesQueryHandler
    : IQueryHandler<ListMyCertificatesQuery, IReadOnlyList<CreatorCertificateResponse>>
{
    private readonly CreatorContext _context;
    private readonly ICreatorCertificateRepository _certificates;

    public ListMyCertificatesQueryHandler(
        CreatorContext context, ICreatorCertificateRepository certificates)
    {
        _context = context;
        _certificates = certificates;
    }

    public async Task<Result<IReadOnlyList<CreatorCertificateResponse>>> Handle(
        ListMyCertificatesQuery query, CancellationToken cancellationToken)
    {
        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorCertificateResponse>>(caller.Error);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        if (creator is null)
        {
            return Result.Success<IReadOnlyList<CreatorCertificateResponse>>([]);
        }

        var certificates = await _certificates.ListForCreatorAsync(creator.Id, cancellationToken);

        return Result.Success<IReadOnlyList<CreatorCertificateResponse>>(
            certificates.Select(certificate => certificate.ToResponse()).ToList());
    }
}

/// <summary>
/// Opens progress rows and settles a finished course.
/// <para>
/// <b>Extracted because three handlers need it and DSP1001 forbids one of them calling another.</b>
/// Completing a lesson, passing a quiz and saving a course all have to find-or-open the same row,
/// and the first two have to settle the same consequence when the last lesson lands.
/// </para>
/// <para>
/// <b>The certificate is issued against a persisted completion and by nothing else.</b> §32 of the
/// phase brief, and the reason there is no route that mints one.
/// </para>
/// </summary>
public sealed class CourseCompletionService
{
    private readonly IAcademyRepository _academy;
    private readonly ICreatorLearningProgressRepository _progress;
    private readonly ICreatorCertificateRepository _certificates;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CourseCompletionService(
        IAcademyRepository academy,
        ICreatorLearningProgressRepository progress,
        ICreatorCertificateRepository certificates,
        IGuidGenerator guids,
        IClock clock)
    {
        _academy = academy;
        _progress = progress;
        _certificates = certificates;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>Finds or opens this creator's progress row for a course. Stages; never saves.</summary>
    public async Task<CreatorLearningProgress> OpenAsync(
        Domain.Creators.CreatorProfile creator,
        AcademyCourse course,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(course);

        var existing = await _progress.FindAsync(creator.Id, course.Id, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var opened = CreatorLearningProgress.Begin(
            CreatorLearningProgressId.From(_guids.NewSequentialGuid()),
            creator.Id,
            creator.UserId,
            course,
            _clock.UtcNow);

        _progress.Add(opened);

        return opened;
    }

    /// <summary>
    /// Re-derives the course state and issues what finishing it earns.
    /// <para>
    /// Stages the certificate and the points grant; the caller saves, so a completion cannot commit
    /// without them.
    /// </para>
    /// </summary>
    public async Task<CreatorCourseProgressResponse> SettleAsync(
        Domain.Creators.CreatorProfile creator,
        AcademyCourse course,
        CreatorLearningProgress progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(progress);

        var lessons = (await _academy.ListLessonsAsync(course.Id, cancellationToken))
            .Select(lesson => lesson.Id)
            .ToList();

        var now = _clock.UtcNow;
        var justFinished = progress.Recompute(lessons, now);

        if (justFinished)
        {
            CreatorCertificateId? certificateId = null;

            if (course.AwardsCertificate)
            {
                var existing = await _certificates.FindForCourseAsync(
                    creator.Id, course.Id, cancellationToken);

                if (existing is null)
                {
                    var reference =
                        $"DCA-{course.Slug.ToUpperInvariant()}-{creator.Id.Value:N}"[..Math.Min(
                            48, $"DCA-{course.Slug.ToUpperInvariant()}-{creator.Id.Value:N}".Length)];

                    var certificate = CreatorCertificate.Issue(
                        CreatorCertificateId.From(_guids.NewSequentialGuid()),
                        creator.Id,
                        creator.UserId,
                        course,
                        creator.DisplayName,
                        reference,
                        now);

                    _certificates.Add(certificate);
                    certificateId = certificate.Id;
                }
                else
                {
                    certificateId = existing.Id;
                }
            }

            // Raises the points-earned domain event exactly once, guarded by the grant reference on
            // the progress row.
            progress.AwardCompletion(
                course,
                certificateId,
                $"course:{course.Slug}:{creator.Id.Value:N}",
                now);
        }

        _progress.Update(progress);

        return progress.ToResponse(course, lessons);
    }
}
