using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>Reads the grant catalogue, with the form every application is validated against.</summary>
public sealed class CommunityGrantRepository : ICommunityGrantRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityGrantRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityGrant> Grants => _dbContext.Set<CommunityGrant>();

    public void Add(CommunityGrant grant) => Grants.Add(grant);

    public async Task<CommunityGrant?> GetAsync(
        CommunityGrantId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Loaded().FirstOrDefaultAsync(grant => grant.Id == id, cancellationToken);
    }

    public async Task<CommunityGrant?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CommunityGrant>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(grant => grant.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Loaded().FirstOrDefaultAsync(grant => grant.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityGrant>> ListAsync(
        string? category,
        string? countryCode,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = Loaded().Where(grant => grant.IsPublished);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalised = category.Trim();
            query = query.Where(grant => grant.Category == normalised);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();

            // An empty country list means the grant is open everywhere, which is why the predicate
            // is "no restriction OR names this country" rather than a plain membership test.
            query = query.Where(grant =>
                !grant.Countries.Any() || grant.Countries.Any(entry => entry.CountryCode == country));
        }

        return await query
            .OrderBy(grant => grant.DeadlineAt ?? DateTimeOffset.MaxValue)
            .ThenBy(grant => grant.Title)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<CommunityGrant> Loaded() =>
        Grants
            .Include(grant => grant.Fields)
            .Include(grant => grant.FieldOptions)
            .Include(grant => grant.Declarations)
            .Include(grant => grant.Deliverables)
            .Include(grant => grant.Countries);
}

/// <summary>Reads and writes grant applications.</summary>
public sealed class GrantApplicationRepository : IGrantApplicationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GrantApplicationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GrantApplication> Applications => _dbContext.Set<GrantApplication>();

    public void Add(GrantApplication application) => Applications.Add(application);

    public void Update(GrantApplication application) => _dbContext.StageUpdate(application);

    public async Task<GrantApplication?> GetAsync(
        GrantApplicationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Loaded().FirstOrDefaultAsync(
            application => application.Id == id, cancellationToken);
    }

    public async Task<GrantApplication?> FindForCreatorAsync(
        CommunityGrantId grantId, CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grantId);
        ArgumentNullException.ThrowIfNull(creatorId);

        var staged = _dbContext.ChangeTracker
            .Entries<GrantApplication>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(application =>
                application.GrantId == grantId && application.CreatorId == creatorId);

        if (staged is not null)
        {
            return staged;
        }

        return await Loaded().FirstOrDefaultAsync(
            application => application.GrantId == grantId && application.CreatorId == creatorId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GrantApplication>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        return await Loaded()
            .Where(application => application.CreatorId == creatorId)
            .OrderByDescending(application => application.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GrantApplication>> ListForReviewAsync(
        CommunityGrantId? grantId,
        GrantApplicationState? state,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = Loaded().AsQueryable();

        if (grantId is not null)
        {
            query = query.Where(application => application.GrantId == grantId);
        }

        if (state is { } wanted)
        {
            query = query.Where(application => application.State == wanted);
        }
        else
        {
            // The reviewer's default queue is what is waiting on them, not everything ever filed.
            query = query.Where(application =>
                application.State == GrantApplicationState.Submitted
                || application.State == GrantApplicationState.UnderReview);
        }

        return await query
            .OrderBy(application => application.SubmittedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<GrantApplication?> FindBySubmitKeyAsync(
        string key, CancellationToken cancellationToken = default) =>
        await Loaded().FirstOrDefaultAsync(
            application => application.SubmitIdempotencyKey == key, cancellationToken);

    private IQueryable<GrantApplication> Loaded() =>
        Applications
            .Include(application => application.Answers)
            .Include(application => application.Attachments)
            .Include(application => application.AcceptedDeclarations)
            .Include(application => application.Reports)
            .Include(application => application.InformationRequests);
}

/// <summary>
/// Reads the Academy's content.
/// <para>
/// <b><see cref="GetQuizAsync"/> is the only method that loads an answer key</b>, and its callers
/// are the grader and the seeder. Every other read serves questions and options without it.
/// </para>
/// </summary>
public sealed class AcademyRepository : IAcademyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AcademyRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AcademySection> Sections => _dbContext.Set<AcademySection>();

    private DbSet<AcademyCourse> Courses => _dbContext.Set<AcademyCourse>();

    private DbSet<AcademyLesson> Lessons => _dbContext.Set<AcademyLesson>();

    private DbSet<AcademyQuiz> Quizzes => _dbContext.Set<AcademyQuiz>();

    public void AddSection(AcademySection section) => Sections.Add(section);

    public void AddCourse(AcademyCourse course) => Courses.Add(course);

    public void AddLesson(AcademyLesson lesson) => Lessons.Add(lesson);

    public void AddQuiz(AcademyQuiz quiz) => Quizzes.Add(quiz);

    public void UpdateCourse(AcademyCourse course) => _dbContext.StageUpdate(course);

    public async Task<IReadOnlyList<AcademySection>> ListSectionsAsync(
        CancellationToken cancellationToken = default) =>
        await Sections
            .Where(section => section.IsPublished)
            .OrderBy(section => section.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AcademyCourse>> ListCoursesAsync(
        string? sectionKey, CourseLevel? level, CancellationToken cancellationToken = default)
    {
        var query = Courses
            .Include(course => course.Tags)
            .Include(course => course.Prerequisites)
            .Where(course => course.IsPublished);

        if (!string.IsNullOrWhiteSpace(sectionKey))
        {
            var key = sectionKey.Trim();
            query = query.Where(course => course.SectionKey == key);
        }

        if (level is { } wanted)
        {
            query = query.Where(course => course.Level == wanted);
        }

        return await query
            .OrderBy(course => course.SortOrder)
            .ThenBy(course => course.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<AcademyCourse?> GetCourseAsync(
        AcademyCourseId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Courses
            .Include(course => course.Tags)
            .Include(course => course.Prerequisites)
            .FirstOrDefaultAsync(course => course.Id == id, cancellationToken);
    }

    public async Task<AcademyCourse?> FindCourseBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<AcademyCourse>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(course => course.Slug == slug);

        if (staged is not null)
        {
            return staged;
        }

        return await Courses
            .Include(course => course.Tags)
            .Include(course => course.Prerequisites)
            .FirstOrDefaultAsync(course => course.Slug == slug, cancellationToken);
    }

    public async Task<IReadOnlyList<AcademyLesson>> ListLessonsAsync(
        AcademyCourseId courseId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(courseId);

        return await Lessons
            .Include(lesson => lesson.Blocks)
            .Include(lesson => lesson.BlockItems)
            .Where(lesson => lesson.CourseId == courseId && lesson.IsPublished)
            .OrderBy(lesson => lesson.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<AcademyLesson?> GetLessonAsync(
        AcademyLessonId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Lessons
            .Include(lesson => lesson.Blocks)
            .Include(lesson => lesson.BlockItems)
            .FirstOrDefaultAsync(lesson => lesson.Id == id, cancellationToken);
    }

    public async Task<AcademyLesson?> FindLessonByKeyAsync(
        AcademyCourseId courseId, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(courseId);

        var staged = _dbContext.ChangeTracker
            .Entries<AcademyLesson>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(lesson => lesson.CourseId == courseId && lesson.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Lessons
            .Include(lesson => lesson.Blocks)
            .Include(lesson => lesson.BlockItems)
            .FirstOrDefaultAsync(
                lesson => lesson.CourseId == courseId && lesson.Key == key, cancellationToken);
    }

    public async Task<AcademyQuiz?> GetQuizAsync(
        AcademyQuizId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Quizzes
            .Include(quiz => quiz.Questions)
            .Include(quiz => quiz.Options)
            .FirstOrDefaultAsync(quiz => quiz.Id == id, cancellationToken);
    }

    public async Task<AcademyQuiz?> FindQuizByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<AcademyQuiz>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(quiz => quiz.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Quizzes
            .Include(quiz => quiz.Questions)
            .Include(quiz => quiz.Options)
            .FirstOrDefaultAsync(quiz => quiz.Key == key, cancellationToken);
    }
}

/// <summary>Reads and writes one creator's progress through a course.</summary>
public sealed class CreatorLearningProgressRepository : ICreatorLearningProgressRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorLearningProgressRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorLearningProgress> Progress => _dbContext.Set<CreatorLearningProgress>();

    public void Add(CreatorLearningProgress progress) => Progress.Add(progress);

    public void Update(CreatorLearningProgress progress) => _dbContext.StageUpdate(progress);

    public async Task<CreatorLearningProgress?> FindAsync(
        CreatorId creatorId, AcademyCourseId courseId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);
        ArgumentNullException.ThrowIfNull(courseId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorLearningProgress>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row => row.CreatorId == creatorId && row.CourseId == courseId);

        if (staged is not null)
        {
            return staged;
        }

        return await Loaded().FirstOrDefaultAsync(
            row => row.CreatorId == creatorId && row.CourseId == courseId, cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorLearningProgress>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        return await Loaded()
            .Where(row => row.CreatorId == creatorId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<CreatorLearningProgress> Loaded() =>
        Progress
            .Include(row => row.CompletedLessons)
            .Include(row => row.Attempts);
}

/// <summary>Reads and writes issued certificates.</summary>
public sealed class CreatorCertificateRepository : ICreatorCertificateRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorCertificateRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorCertificate> Certificates => _dbContext.Set<CreatorCertificate>();

    public void Add(CreatorCertificate certificate) => Certificates.Add(certificate);

    public async Task<IReadOnlyList<CreatorCertificate>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        return await Certificates
            .AsNoTracking()
            .Where(certificate => certificate.CreatorId == creatorId)
            .OrderByDescending(certificate => certificate.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreatorCertificate?> FindForCourseAsync(
        CreatorId creatorId, AcademyCourseId courseId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);
        ArgumentNullException.ThrowIfNull(courseId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorCertificate>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row => row.CreatorId == creatorId && row.CourseId == courseId);

        if (staged is not null)
        {
            return staged;
        }

        return await Certificates.FirstOrDefaultAsync(
            certificate => certificate.CreatorId == creatorId && certificate.CourseId == courseId,
            cancellationToken);
    }
}
