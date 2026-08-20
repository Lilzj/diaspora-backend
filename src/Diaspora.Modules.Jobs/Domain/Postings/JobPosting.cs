using System.Collections.ObjectModel;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// A vacancy an employer is advertising. The aggregate the whole module is arranged around.
/// <para>
/// <b>The posting is owned by an employer, not by a person.</b> That single decision is what
/// makes this a recruitment platform rather than a classifieds board: a recruiter who leaves
/// does not take the vacancy with them, a colleague can pick up the pipeline, and the trust
/// badge a candidate is relying on belongs to the organisation that will actually employ them.
/// <see cref="CreatedByUserId"/> records who wrote it, and grants nothing.
/// </para>
/// <para>
/// <b>What it owns:</b> everything whose lifetime is exactly the posting's and whose consistency
/// has to hold in one transaction — its prose, its bulleted lists, its skills, tags, media,
/// provider and guide references, its screening questions and its moderation trail.
/// <b>What it deliberately does not own:</b> applications, interviews, offers, the hiring
/// pipeline, saved-job rows and analytics are separate aggregates keyed by
/// <see cref="JobPostingId"/>, because a posting with four thousand applicants must never be
/// loaded to accept the four-thousand-and-first. Comments and reports are not here at all —
/// they belong to Community, reached through the content-source seam.
/// </para>
/// <para>
/// <b>Concurrency is a revision number, not a timestamp.</b> Every content mutation bumps
/// <see cref="Revision"/>; an editor sends back the revision it loaded and
/// <see cref="EnsureRevision"/> rejects a stale save with a conflict the client can merge from.
/// Counter mutations deliberately do not bump it — a candidate viewing a job is not an edit,
/// and must never make a recruiter's in-flight save fail.
/// </para>
/// <para>
/// <b>Verification is a projection.</b> Nothing here can grant a trust mark.
/// <see cref="SubmitForVerification"/> records that a claim went to Trust;
/// <see cref="ApplyVerificationDecision"/> is reachable only from a handler for a Trust
/// integration event, and is guarded on the decision timestamp because delivery is
/// at-least-once and unordered.
/// </para>
/// </summary>
public sealed class JobPosting
    : AggregateRoot<JobPostingId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<JobDetailLine> _detailLines = [];
    private readonly List<JobSkillRequirement> _skills = [];
    private readonly List<JobTagAssignment> _tags = [];
    private readonly List<JobMediaReference> _mediaReferences = [];
    private readonly List<JobProviderReference> _providerReferences = [];
    private readonly List<JobGuideReference> _guideReferences = [];
    private readonly List<ScreeningQuestion> _screeningQuestions = [];
    private readonly List<JobModerationNote> _moderationNotes = [];

    private JobPosting(
        JobPostingId id,
        EmployerProfileId employerId,
        UserId createdByUserId,
        JobTitle title,
        JobSlug slug,
        JobDescription description,
        string categoryKey,
        JobType jobType,
        ExperienceLevel experienceLevel,
        JobLocationContext location,
        WorkArrangement workArrangement,
        DateTimeOffset createdAt)
        : base(id)
    {
        EmployerId = employerId;
        CreatedByUserId = createdByUserId;
        Title = title;
        Slug = slug;
        Description = description;
        CategoryKey = categoryKey;
        JobType = jobType;
        ExperienceLevel = experienceLevel;
        Location = location;
        WorkArrangement = workArrangement;
        Salary = SalaryRange.Undisclosed;
        Status = JobStatus.Draft;
        Visibility = JobVisibility.Public;
        ModerationState = JobModerationState.Clear;
        TrustMark = JobTrustMark.None;
        Engagement = JobEngagement.None;
        EducationLevel = EducationLevel.None;
        VacancyCount = 1;
        CreatedAt = createdAt;
    }

    private JobPosting() { }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    /// <summary>The organisation hiring. The posting belongs to it, not to a person.</summary>
    public EmployerProfileId EmployerId { get; private set; } = default!;

    /// <summary>
    /// Who wrote it. Recorded for the audit trail and grants nothing — authority over a posting
    /// comes from employer membership, which the employer aggregate answers.
    /// </summary>
    public UserId CreatedByUserId { get; private set; } = default!;

    /// <summary>
    /// The platform's ownership seam. The employer, so the kernel's ownership checks agree with
    /// the module's own.
    /// </summary>
    public Guid OwnerId => EmployerId.Value;

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public JobTitle Title { get; private set; } = default!;

    /// <summary>The stable handle a deep link uses. Allocated once and never recomputed.</summary>
    public JobSlug Slug { get; private set; } = default!;

    public JobDescription Description { get; private set; } = default!;

    /// <summary>The employment domain, validated against the catalogue by the handler.</summary>
    public string CategoryKey { get; private set; } = string.Empty;

    public JobType JobType { get; private set; }

    public ExperienceLevel ExperienceLevel { get; private set; }

    public EducationLevel EducationLevel { get; private set; }

    public JobLocationContext Location { get; private set; } = default!;

    public WorkArrangement WorkArrangement { get; private set; } = default!;

    public SalaryRange Salary { get; private set; } = default!;

    /// <summary>Seats advertised. Filling all of them is what moves the posting to Filled.</summary>
    public int VacancyCount { get; private set; }

    /// <summary>Seats taken so far.</summary>
    public int FilledCount { get; private set; }

    /// <summary>Whether an application without a résumé is refused.</summary>
    public bool RequiresResume { get; private set; }

    // ── Lifecycle & audience ────────────────────────────────────────────────────────────────

    public JobStatus Status { get; private set; }

    public JobVisibility Visibility { get; private set; }

    public JobModerationState ModerationState { get; private set; }

    /// <summary>Staff-set placement. Never something an employer can grant itself.</summary>
    public bool IsFeatured { get; private set; }

    public DateTimeOffset? FeaturedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    /// <summary>When applications stop. Null means open until the employer closes it.</summary>
    public DateTimeOffset? ClosingAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset? FilledAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Why the employer closed it. Shown to applicants, who are owed an explanation.</summary>
    public string? ClosureReason { get; private set; }

    // ── Denormalised blocks ─────────────────────────────────────────────────────────────────

    public JobTrustMark TrustMark { get; private set; } = default!;

    public JobEngagement Engagement { get; private set; } = default!;

    /// <summary>The optimistic concurrency token. Bumped by every content mutation.</summary>
    public int Revision { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every bulleted line, of every kind, unordered.
    /// <para>
    /// The persistence navigation, and the reason it is public: the three lists below are
    /// filtered views over one owned collection, and EF can only map a navigation it can also
    /// write to. Callers want <see cref="Responsibilities"/>, <see cref="Requirements"/> or
    /// <see cref="Benefits"/>; this exists so the mapping has something to bind.
    /// </para>
    /// </summary>
    public IReadOnlyList<JobDetailLine> DetailLines => _detailLines.AsReadOnly();

    public IReadOnlyList<JobDetailLine> Responsibilities => DetailsOf(JobDetailKind.Responsibility);

    public IReadOnlyList<JobDetailLine> Requirements => DetailsOf(JobDetailKind.Requirement);

    public IReadOnlyList<JobDetailLine> Benefits => DetailsOf(JobDetailKind.Benefit);

    public IReadOnlyList<JobSkillRequirement> Skills =>
        _skills.OrderBy(skill => skill.Order).ToList().AsReadOnly();

    public IReadOnlyList<JobTagAssignment> Tags =>
        _tags.OrderBy(tag => tag.Order).ToList().AsReadOnly();

    public IReadOnlyList<JobMediaReference> MediaReferences =>
        _mediaReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<JobProviderReference> ProviderReferences =>
        _providerReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<JobGuideReference> GuideReferences =>
        _guideReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<ScreeningQuestion> ScreeningQuestions =>
        _screeningQuestions.OrderBy(question => question.Order).ToList().AsReadOnly();

    public IReadOnlyList<JobModerationNote> ModerationNotes =>
        _moderationNotes.OrderBy(note => note.CreatedAt).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The bar for publication: a title, a body, a category and a country. Deliberately low —
    /// an employer who cannot clear it simply does not publish, and a job board with fewer real
    /// vacancies on it helps nobody. Everything richer is optional and the card degrades.
    /// </summary>
    public bool IsPublishable =>
        CategoryKey.Length > 0
        && Title.Value.Length > 0
        && Description.Body.Length > 0
        && Location.CountryCode.Length > 0;

    /// <summary>Whether the posting may appear in search, browse, alerts and nearby results.</summary>
    public bool IsDiscoverable =>
        !IsDeleted
        && JobStatusTransitions.IsPubliclyVisible(Status)
        && Visibility == JobVisibility.Public
        && ModerationState != JobModerationState.Hidden;

    /// <summary>Seats still to fill.</summary>
    public int RemainingVacancies => Math.Max(0, VacancyCount - FilledCount);

    /// <summary>
    /// Whether an application would be accepted right now. Takes the clock, because a posting
    /// past its closing date is shut whether or not anything has run to notice.
    /// </summary>
    public bool AcceptsApplications(DateTimeOffset now) =>
        !IsDeleted
        && ModerationState != JobModerationState.Hidden
        && JobStatusTransitions.AcceptsApplications(Status)
        && RemainingVacancies > 0
        && (ClosingAt is not { } closing || closing > now);

    /// <summary>Whether the closing date has passed. Drives the expiry sweep.</summary>
    public bool HasClosingDatePassed(DateTimeOffset now) =>
        ClosingAt is { } closing && closing <= now;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a posting as a draft, visible only to the employer's own team.
    /// <para>
    /// A draft rather than a live vacancy, deliberately: postings are written over sessions and
    /// usually reviewed by someone else before they go out, and publishing straight from
    /// creation would put half-written adverts in front of people deciding where to spend an
    /// afternoon. Ids and clock are passed in — the aggregate never generates either.
    /// </para>
    /// </summary>
    public static Result<JobPosting> Create(
        JobPostingId id,
        EmployerProfileId employerId,
        UserId createdByUserId,
        JobTitle title,
        JobSlug slug,
        JobDescription description,
        string? categoryKey,
        JobType jobType,
        ExperienceLevel experienceLevel,
        JobLocationContext location,
        WorkArrangement workArrangement,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(employerId);
        ArgumentNullException.ThrowIfNull(createdByUserId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(workArrangement);

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure<JobPosting>(JobErrors.CategoryKeyInvalid);
        }

        if (!Enum.IsDefined(jobType) || !Enum.IsDefined(experienceLevel))
        {
            return Result.Failure<JobPosting>(JobErrors.CategoryKeyInvalid);
        }

        var posting = new JobPosting(
            id, employerId, createdByUserId, title, slug, description, key,
            jobType, experienceLevel, location, workArrangement, now);

        posting.RaiseDomainEvent(new JobPostingCreatedDomainEvent
        {
            PostingId = id,
            EmployerId = employerId,
            CreatedBy = createdByUserId,
            Title = title.Value,
            Slug = slug.Value,
            CategoryKey = key,
            CountryCode = location.CountryCode
        });

        return Result.Success(posting);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rejects a save based on a revision the editor no longer holds.
    /// <para>
    /// Null means the caller opted out — an internal write, or a client that genuinely wants
    /// last-writer-wins. A conflict is a <see cref="ErrorType.Conflict"/> so the client can
    /// offer a merge rather than an error page.
    /// </para>
    /// </summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the posting's descriptive fields — everything a candidate reads that is not a
    /// bulleted list.
    /// <para>
    /// Reports which fields actually moved, so a consumer can ignore a change it does not care
    /// about. A salary edit matters to a standing alert; a tag edit does not.
    /// </para>
    /// </summary>
    public Result UpdateDetails(
        JobTitle title,
        JobDescription description,
        JobType jobType,
        ExperienceLevel experienceLevel,
        EducationLevel educationLevel,
        JobLocationContext location,
        WorkArrangement workArrangement,
        SalaryRange salary,
        JobVisibility visibility,
        int vacancyCount,
        bool requiresResume,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(workArrangement);
        ArgumentNullException.ThrowIfNull(salary);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (vacancyCount < 1 || vacancyCount > JobLimits.MaxVacancies)
        {
            return Result.Failure(JobErrors.VacanciesInvalid);
        }

        // Seats already taken cannot exceed seats advertised. An employer reducing the count
        // below what they have filled is correcting the wrong number.
        if (vacancyCount < FilledCount)
        {
            return Result.Failure(JobErrors.VacanciesInvalid);
        }

        // A volunteer posting that quotes a salary is either mislabelled or misleading, and
        // both are worth refusing at the point the employer can still fix it.
        if (jobType == JobType.Volunteer && salary.IsDisclosed && !salary.IsUnpaid)
        {
            return Result.Failure(JobErrors.SalaryInvalid);
        }

        var changed = new List<string>();

        Apply(!Title.Equals(title), "title", () => Title = title, changed);
        Apply(!Description.Equals(description), "description", () => Description = description, changed);
        Apply(JobType != jobType, "type", () => JobType = jobType, changed);
        Apply(ExperienceLevel != experienceLevel, "experience", () => ExperienceLevel = experienceLevel, changed);
        Apply(EducationLevel != educationLevel, "education", () => EducationLevel = educationLevel, changed);
        Apply(!Location.Equals(location), "location", () => Location = location, changed);
        Apply(!WorkArrangement.Equals(workArrangement), "workplace", () => WorkArrangement = workArrangement, changed);
        Apply(!Salary.Equals(salary), "salary", () => Salary = salary, changed);
        Apply(Visibility != visibility, "visibility", () => Visibility = visibility, changed);
        Apply(VacancyCount != vacancyCount, "vacancies", () => VacancyCount = vacancyCount, changed);
        Apply(RequiresResume != requiresResume, "resume-required", () => RequiresResume = requiresResume, changed);

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();
        RaiseUpdated(changed, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces one of the three bulleted lists wholesale.
    /// <para>
    /// Wholesale rather than per-line, because that is how the editor works: a person writes the
    /// list and the client saves it, and a per-line API would make every save a diff protocol
    /// with a partial-failure story. It is also what makes the count invariant trivially correct
    /// — the lines are validated against each other as a set.
    /// </para>
    /// </summary>
    public Result SetDetailLines(
        JobDetailKind kind,
        IReadOnlyList<JobDetailDraft> lines,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (!Enum.IsDefined(kind))
        {
            return Result.Failure(JobErrors.RequirementInvalid);
        }

        var ceiling = kind switch
        {
            JobDetailKind.Responsibility => JobLimits.MaxResponsibilities,
            JobDetailKind.Requirement => JobLimits.MaxRequirements,
            _ => JobLimits.MaxBenefits
        };

        if (lines.Count > ceiling)
        {
            return Result.Failure(LimitErrorFor(kind));
        }

        var rebuilt = new List<JobDetailLine>(lines.Count);
        var order = 0;

        foreach (var draft in lines)
        {
            if (draft is null)
            {
                return Result.Failure(InvalidErrorFor(kind));
            }

            var line = JobDetailLine.Create(
                draft.LineId, kind, draft.Text, order, draft.IsEssential);

            if (line is null)
            {
                return Result.Failure(InvalidErrorFor(kind));
            }

            rebuilt.Add(line);
            order++;
        }

        _detailLines.RemoveAll(existing => existing.Kind == kind);
        _detailLines.AddRange(rebuilt);

        Touch();
        RaiseUpdated([kind.ToString().ToLowerInvariant()], changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the skill set wholesale. Deduplicated on the normalised key.</summary>
    public Result SetSkills(
        IReadOnlyList<JobSkillDraft> skills, int? baseRevision, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (skills.Count > JobLimits.MaxSkills)
        {
            return Result.Failure(JobErrors.SkillLimit);
        }

        var rebuilt = new List<JobSkillRequirement>(skills.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in skills)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.SkillInvalid);
            }

            var skill = JobSkillRequirement.Create(
                draft.Name, draft.MinimumProficiency, draft.IsEssential, rebuilt.Count);

            if (skill is null)
            {
                return Result.Failure(JobErrors.SkillInvalid);
            }

            // A repeated skill is a paste artefact, not an error worth refusing the save over.
            if (seen.Add(skill.SkillKey))
            {
                rebuilt.Add(skill);
            }
        }

        _skills.Clear();
        _skills.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["skills"], changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the tag set wholesale — that is how the client's chip editor saves.</summary>
    public Result SetTags(IReadOnlyList<string?> tags, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in tags)
        {
            var slug = JobTagAssignment.NormalizeSlug(raw);

            if (slug is null)
            {
                return Result.Failure(JobErrors.TagInvalid);
            }

            if (seen.Add(slug))
            {
                resolved.Add(slug);
            }
        }

        if (resolved.Count > JobLimits.MaxTags)
        {
            return Result.Failure(JobErrors.TagLimit);
        }

        _tags.Clear();
        _tags.AddRange(resolved.Select(JobTagAssignment.Create));

        Touch();
        RaiseUpdated(["tags"], changedBy: null);

        return Result.Success();
    }

    /// <summary>Replaces the screening questions wholesale.</summary>
    public Result SetScreeningQuestions(
        IReadOnlyList<ScreeningQuestionDraft> questions, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(questions);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (questions.Count > JobLimits.MaxScreeningQuestions)
        {
            return Result.Failure(JobErrors.AnswerLimit);
        }

        var rebuilt = new List<ScreeningQuestion>(questions.Count);
        var order = 0;

        foreach (var draft in questions)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.AnswerInvalid);
            }

            var question = ScreeningQuestion.Create(
                draft.QuestionId, draft.Text, draft.IsRequired, order);

            if (question is null)
            {
                return Result.Failure(JobErrors.AnswerInvalid);
            }

            rebuilt.Add(question);
            order++;
        }

        _screeningQuestions.Clear();
        _screeningQuestions.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["questions"], changedBy: null);

        return Result.Success();
    }

    /// <summary>Moves the posting to another employment domain.</summary>
    public Result ChangeCategory(string? categoryKey, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure(JobErrors.CategoryKeyInvalid);
        }

        if (string.Equals(CategoryKey, key, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        CategoryKey = key;
        Touch();
        RaiseUpdated(["category"], changedBy: null);

        return Result.Success();
    }

    /// <summary>
    /// Sets or clears the closing date.
    /// <para>
    /// Extending a date on an expired posting reopens it, which is the behaviour an employer
    /// expects and the reason expiry is modelled as reversible.
    /// </para>
    /// </summary>
    public Result SetClosingDate(DateTimeOffset? closingAt, DateTimeOffset now, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (closingAt is { } closing && closing <= now)
        {
            return Result.Failure(JobErrors.ClosingDateInPast);
        }

        if (ClosingAt == closingAt)
        {
            return Result.Success();
        }

        ClosingAt = closingAt;
        Touch();
        RaiseUpdated(["closing-date"], changedBy: null);

        // A future date on an expired posting brings it back. Anything else would make the
        // employer archive and recreate, losing every application already attached.
        if (Status == JobStatus.Expired && closingAt is not null)
        {
            return MoveTo(JobStatus.Open, now, changedBy: null, reason: null);
        }

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // References
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Attaches a media asset by reference. Idempotent on (media, role).</summary>
    public Result AttachMedia(Guid mediaId, JobMediaRole role, string? caption = null)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (mediaId == Guid.Empty || !Enum.IsDefined(role))
        {
            return Result.Failure(JobErrors.MediaInvalid);
        }

        if (_mediaReferences.Exists(existing =>
                existing.MediaId == mediaId && existing.Role == role))
        {
            return Result.Success();
        }

        if (_mediaReferences.Count >= JobLimits.MaxMediaReferences)
        {
            return Result.Failure(JobErrors.MediaLimit);
        }

        _mediaReferences.Add(JobMediaReference.Create(
            mediaId, role, _mediaReferences.Count(existing => existing.Role == role), caption));

        Touch();

        RaiseDomainEvent(new JobPostingMediaAttachedDomainEvent
        {
            PostingId = Id,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Detaches a media asset. Idempotent — detaching what is not there succeeds.</summary>
    public Result DetachMedia(Guid mediaId, JobMediaRole role)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _mediaReferences.Find(existing =>
            existing.MediaId == mediaId && existing.Role == role);

        if (reference is null)
        {
            return Result.Success();
        }

        _mediaReferences.Remove(reference);
        Touch();

        RaiseDomainEvent(new JobPostingMediaDetachedDomainEvent
        {
            PostingId = Id,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Points candidates at a provider. Idempotent on the provider identifier.</summary>
    public Result AddProviderReference(Guid providerId, string? role, string? note)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (providerId == Guid.Empty)
        {
            return Result.Failure(JobErrors.ProviderReferenceInvalid);
        }

        if (_providerReferences.Exists(existing => existing.ProviderId == providerId))
        {
            return Result.Success();
        }

        if (_providerReferences.Count >= JobLimits.MaxProviderReferences)
        {
            return Result.Failure(JobErrors.ProviderReferenceLimit);
        }

        _providerReferences.Add(JobProviderReference.Create(
            providerId, role, note, _providerReferences.Count));

        Touch();

        return Result.Success();
    }

    /// <summary>Removes a provider reference. Idempotent.</summary>
    public Result RemoveProviderReference(Guid providerId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _providerReferences.Find(existing => existing.ProviderId == providerId);

        if (reference is null)
        {
            return Result.Success();
        }

        _providerReferences.Remove(reference);
        Touch();

        return Result.Success();
    }

    /// <summary>Links a guide beside the posting. Idempotent on the guide identifier.</summary>
    public Result AddGuideReference(Guid guideId, string? label, bool isAutomatic = false)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (guideId == Guid.Empty)
        {
            return Result.Failure(JobErrors.GuideReferenceInvalid);
        }

        if (_guideReferences.Exists(existing => existing.GuideId == guideId))
        {
            return Result.Success();
        }

        if (_guideReferences.Count >= JobLimits.MaxGuideReferences)
        {
            return Result.Failure(JobErrors.GuideReferenceLimit);
        }

        _guideReferences.Add(JobGuideReference.Create(
            guideId, label, isAutomatic, _guideReferences.Count));

        Touch();

        return Result.Success();
    }

    /// <summary>Removes a guide reference. Idempotent.</summary>
    public Result RemoveGuideReference(Guid guideId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _guideReferences.Find(existing => existing.GuideId == guideId);

        if (reference is null)
        {
            return Result.Success();
        }

        _guideReferences.Remove(reference);
        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Replaces every platform-suggested guide link, leaving the employer's own choices alone.
    /// <para>
    /// The split is the whole point of <see cref="JobGuideReference.IsAutomatic"/>: suggestions
    /// are refreshed when the posting's country or category changes, and a refresh must never
    /// discard a link a person deliberately added.
    /// </para>
    /// </summary>
    public Result ReplaceAutomaticGuideReferences(IReadOnlyList<(Guid GuideId, string? Label)> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        _guideReferences.RemoveAll(reference => reference.IsAutomatic);

        var order = _guideReferences.Count;

        foreach (var (guideId, label) in suggestions)
        {
            if (guideId == Guid.Empty
                || _guideReferences.Count >= JobLimits.MaxGuideReferences
                || _guideReferences.Exists(existing => existing.GuideId == guideId))
            {
                continue;
            }

            _guideReferences.Add(JobGuideReference.Create(guideId, label, isAutomatic: true, order));
            order++;
        }

        // Deliberately no Touch(): a platform-generated suggestion is not an editor's change and
        // must not invalidate a recruiter's in-flight save.
        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Publishes the posting, optionally opening it for applications at once.
    /// <para>
    /// Guarded by <see cref="IsPublishable"/>, because an empty advert in a search result is
    /// worse than no advert. <paramref name="openImmediately"/> is the ordinary case; publishing
    /// without opening is the employer who wants the role visible before the window starts.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy = null, bool openImmediately = true)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        if (!IsPublishable)
        {
            return Result.Failure(JobErrors.PostingNotPublishable);
        }

        if (ClosingAt is { } closing && closing <= now)
        {
            return Result.Failure(JobErrors.ClosingDateInPast);
        }

        var target = openImmediately ? JobStatus.Open : JobStatus.Published;

        if (Status == target)
        {
            return Result.Failure(JobErrors.PostingAlreadyPublished);
        }

        if (!JobStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(JobErrors.PostingAlreadyPublished);
        }

        var isFirstPublication = FirstPublishedAt is null;
        var previous = Status;

        Status = target;
        PublishedAt = now;
        FirstPublishedAt ??= now;
        ClosedAt = null;
        ClosureReason = null;
        ArchivedAt = null;
        Touch();

        RaiseStatusChanged(previous, target, now, publishedBy, reason: null);

        RaiseDomainEvent(new JobPostingPublishedDomainEvent
        {
            PostingId = Id,
            EmployerId = EmployerId,
            Title = Title.Value,
            Slug = Slug.Value,
            CategoryKey = CategoryKey,
            CountryCode = Location.CountryCode,
            PublishedBy = publishedBy,
            IsFirstPublication = isFirstPublication,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Moves the posting to another lifecycle state, subject to
    /// <see cref="JobStatusTransitions"/>.
    /// <para>
    /// The single mutator every lifecycle command goes through, so the transition table is
    /// consulted in one place rather than at a dozen call sites. Filling and archiving have
    /// their own methods because they do more than move the status.
    /// </para>
    /// </summary>
    public Result MoveTo(
        JobStatus target, DateTimeOffset now, UserId? changedBy, string? reason)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        if (!JobStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(ErrorForRefusedTransition(target));
        }

        var previous = Status;
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmedReason is { Length: > JobLimits.MaxBulletLength })
        {
            trimmedReason = trimmedReason[..JobLimits.MaxBulletLength];
        }

        Status = target;

        switch (target)
        {
            case JobStatus.Filled:
                FilledAt = now;
                ClosedAt ??= now;
                break;

            case JobStatus.Expired:
                ClosedAt ??= now;
                break;

            case JobStatus.Archived:
                ArchivedAt = now;
                ClosedAt ??= now;

                // An archived posting keeps no featured placement: it is off every surface a
                // placement would apply to, and leaving the flag set would resurface it the
                // moment it was restored.
                IsFeatured = false;
                FeaturedAt = null;
                break;

            case JobStatus.Open:
            case JobStatus.ReceivingApplications:
                ClosedAt = null;
                ClosureReason = null;
                ArchivedAt = null;
                break;
        }

        if (trimmedReason is not null)
        {
            ClosureReason = trimmedReason;
        }

        Touch();
        RaiseStatusChanged(previous, target, now, changedBy, trimmedReason);

        return Result.Success();
    }

    /// <summary>
    /// Records that a seat has been taken, and closes the posting when the last one goes.
    /// <para>
    /// Called from the application aggregate's acceptance path. A posting advertising six seats
    /// fills five of them without its status changing at all — which is why the vacancy event
    /// and the status transition are separate facts.
    /// </para>
    /// </summary>
    public Result FillVacancy(JobApplicationId applicationId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(applicationId);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        if (RemainingVacancies == 0)
        {
            return Result.Failure(JobErrors.NoVacanciesRemaining);
        }

        FilledCount++;
        Touch();

        RaiseDomainEvent(new JobPostingVacancyFilledDomainEvent
        {
            PostingId = Id,
            ApplicationId = applicationId,
            FilledCount = FilledCount,
            VacancyCount = VacancyCount,
            FilledAt = now
        });

        return RemainingVacancies == 0
            ? MoveTo(JobStatus.Filled, now, changedBy: null, reason: null)
            : Result.Success();
    }

    /// <summary>
    /// Nudges an open posting into <see cref="JobStatus.ReceivingApplications"/> on the first
    /// application. A no-op everywhere else, because it is called speculatively.
    /// </summary>
    public Result NoteApplicationReceived(DateTimeOffset now)
    {
        Engagement = Engagement.WithApplicationDelta(1);

        return Status == JobStatus.Open
            ? MoveTo(JobStatus.ReceivingApplications, now, changedBy: null, reason: null)
            : Result.Success();
    }

    /// <summary>Expires the posting because its closing date passed. The sweep's entry point.</summary>
    public Result Expire(DateTimeOffset now)
    {
        if (!HasClosingDatePassed(now))
        {
            return Result.Success();
        }

        return JobStatusTransitions.CanTransition(Status, JobStatus.Expired)
            ? MoveTo(JobStatus.Expired, now, changedBy: null, reason: null)
            : Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Featuring
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Staff give the posting featured placement. Publicly visible postings only.</summary>
    public Result Feature(DateTimeOffset now, UserId featuredBy)
    {
        ArgumentNullException.ThrowIfNull(featuredBy);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        if (!JobStatusTransitions.IsPubliclyVisible(Status))
        {
            return Result.Failure(JobErrors.PostingNotPublished);
        }

        if (IsFeatured)
        {
            return Result.Success();
        }

        IsFeatured = true;
        FeaturedAt = now;
        Touch();

        RaiseDomainEvent(new JobPostingFeaturedDomainEvent
        {
            PostingId = Id,
            IsFeatured = true,
            ChangedBy = featuredBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>Withdraws featured placement. Idempotent.</summary>
    public Result Unfeature(DateTimeOffset now, UserId? changedBy = null)
    {
        if (!IsFeatured)
        {
            return Result.Success();
        }

        IsFeatured = false;
        FeaturedAt = null;
        Touch();

        RaiseDomainEvent(new JobPostingFeaturedDomainEvent
        {
            PostingId = Id,
            IsFeatured = false,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification — projection of Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that a verification claim has gone to Trust. Says nothing about the outcome.
    /// </summary>
    /// <param name="verificationId">
    /// Trust's case identifier, when submission returned one.
    /// <para>
    /// <b>Stamping it here is what lets an outcome find this posting again.</b> Trust's events
    /// name a member, and a member may recruit for several employers with many postings each,
    /// so the correlation has to be the case. A claim submitted without recording its identifier
    /// is a claim whose approval arrives with nowhere to land.
    /// </para>
    /// </param>
    public Result SubmitForVerification(
        DateTimeOffset now, UserId requestedBy, Guid? verificationId = null)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        if (!JobStatusTransitions.IsPubliclyVisible(Status))
        {
            return Result.Failure(JobErrors.PostingNotPublished);
        }

        if (TrustMark.State == JobVerificationState.Pending)
        {
            return Result.Failure(JobErrors.VerificationAlreadyPending);
        }

        TrustMark = JobTrustMark.Pending(now, verificationId);
        Touch();

        RaiseDomainEvent(new JobPostingVerificationRequestedDomainEvent
        {
            PostingId = Id,
            EmployerId = EmployerId,
            RequestedBy = requestedBy,
            VerificationId = verificationId,
            RequestedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies an outcome Trust decided. The only path to a verified mark, reachable only from
    /// a handler for a Trust integration event.
    /// <para>
    /// Guarded on <paramref name="decidedAt"/> because delivery is at-least-once and unordered:
    /// a delayed redelivery of an old approval must not re-verify a posting Trust has since
    /// revoked. A stale decision is dropped silently — success, because from the handler's point
    /// of view the event was processed correctly.
    /// </para>
    /// </summary>
    public Result ApplyVerificationDecision(
        JobVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset decidedAt)
    {
        if (TrustMark.IsNewerThan(decidedAt))
        {
            return Result.Success();
        }

        var previous = TrustMark.State;

        TrustMark = state switch
        {
            JobVerificationState.Verified =>
                JobTrustMark.Verified(verificationId, badge, decidedAt),
            JobVerificationState.Revoked =>
                JobTrustMark.Revoked(verificationId, reason, decidedAt),
            JobVerificationState.Rejected =>
                JobTrustMark.Rejected(verificationId, reason, decidedAt),
            JobVerificationState.Pending =>
                JobTrustMark.Pending(decidedAt, verificationId),
            _ => JobTrustMark.Cleared(reason, decidedAt)
        };

        // Deliberately no Touch(): a Trust decision is not an editor's change, and must not
        // make a recruiter's in-flight save fail with a conflict they cannot explain.
        if (previous != TrustMark.State)
        {
            RaiseDomainEvent(new JobPostingVerificationChangedDomainEvent
            {
                PostingId = Id,
                PreviousState = previous,
                CurrentState = TrustMark.State,
                VerificationId = verificationId,
                Badge = badge,
                Reason = reason,
                ChangedAt = decidedAt
            });
        }

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Removes the posting from every public surface, reversibly. The posting's own state is
    /// untouched — lifting the decision restores exactly what was there.
    /// </summary>
    public Result Hide(string? reason, UserId moderatorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > JobLimits.MaxModerationNote)
        {
            return Result.Failure(JobErrors.ModerationNoteInvalid);
        }

        return SetModeration(JobModerationState.Hidden, moderatorId, trimmed, now);
    }

    /// <summary>Lifts a moderation hide. Idempotent.</summary>
    public Result Unhide(UserId? moderatorId, DateTimeOffset now) =>
        ModerationState == JobModerationState.Hidden
            ? SetModeration(JobModerationState.Clear, moderatorId, reason: null, now)
            : Result.Success();

    /// <summary>Puts the posting in the moderation queue without taking it down.</summary>
    public Result Flag(UserId? flaggedBy, string? reason, DateTimeOffset now) =>
        ModerationState == JobModerationState.Clear
            ? SetModeration(JobModerationState.Flagged, flaggedBy, reason, now)
            : Result.Success();

    /// <summary>Appends to the editorial audit trail. The trail is never edited or pruned.</summary>
    public Result AddModerationNote(
        Guid noteId, UserId authorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        var trimmed = note?.Trim();

        if (noteId == Guid.Empty
            || string.IsNullOrEmpty(trimmed)
            || trimmed.Length > JobLimits.MaxModerationNote)
        {
            return Result.Failure(JobErrors.ModerationNoteInvalid);
        }

        if (_moderationNotes.Count >= JobLimits.MaxModerationNotes)
        {
            return Result.Failure(JobErrors.ModerationNoteInvalid);
        }

        _moderationNotes.Add(JobModerationNote.Create(noteId, authorId, trimmed, now));

        // No Touch(): a moderator's note is not a content edit.
        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Engagement counters — never a revision bump, never an update event
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A candidate viewed the posting. Not an edit — no revision bump, no event.</summary>
    public void RecordView(bool unique = false) => Engagement = Engagement.WithView(unique);

    /// <summary>A candidate saved or unsaved it. Applied as a delta by the saved-job handler.</summary>
    public void ApplySaveDelta(int delta) => Engagement = Engagement.WithSaveDelta(delta);

    /// <summary>An application was withdrawn or deleted. Keeps the card's count honest.</summary>
    public void ApplyApplicationDelta(int delta) =>
        Engagement = Engagement.WithApplicationDelta(delta);

    /// <summary>A candidate shared it.</summary>
    public void RecordShare() => Engagement = Engagement.WithShare();

    /// <summary>The posting appeared in a result set.</summary>
    public void RecordImpressions(long count) => Engagement = Engagement.WithImpressions(count);

    /// <summary>Someone opened it from a result set.</summary>
    public void RecordSearchClick() => Engagement = Engagement.WithSearchClick();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the posting deleted. Sets the fields directly rather than relying on the change
    /// tracker, for the reason B6 records: a delete that happens in the tracker cannot also
    /// raise an event, and downstream de-indexing needs one.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new JobPostingDeletedDomainEvent
        {
            PostingId = Id,
            EmployerId = EmployerId,
            DeletedAt = timestamp
        });
    }

    /// <summary>Undoes a soft delete. The posting resumes in whatever status it held.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        RaiseDomainEvent(new JobPostingUndeletedDomainEvent
        {
            PostingId = Id,
            EmployerId = EmployerId
        });
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private void Touch() => Revision++;

    /// <summary>The shared guard for every edit: not deleted, not archived, revision current.</summary>
    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.PostingDeleted);
        }

        if (Status == JobStatus.Archived)
        {
            return Result.Failure(JobErrors.PostingArchivedImmutable);
        }

        return EnsureRevision(baseRevision);
    }

    private ReadOnlyCollection<JobDetailLine> DetailsOf(JobDetailKind kind) =>
        _detailLines.Where(line => line.Kind == kind)
            .OrderBy(line => line.Order)
            .ToList()
            .AsReadOnly();

    private Result SetModeration(
        JobModerationState target, UserId? actorId, string? reason, DateTimeOffset now)
    {
        if (ModerationState == target)
        {
            return Result.Success();
        }

        var previous = ModerationState;
        ModerationState = target;

        RaiseDomainEvent(new JobPostingModerationChangedDomainEvent
        {
            PostingId = Id,
            PreviousState = previous,
            CurrentState = target,
            ModeratorId = actorId,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    private void RaiseStatusChanged(
        JobStatus previous,
        JobStatus current,
        DateTimeOffset now,
        UserId? changedBy,
        string? reason) =>
        RaiseDomainEvent(new JobPostingStatusChangedDomainEvent
        {
            PostingId = Id,
            EmployerId = EmployerId,
            PreviousStatus = previous,
            CurrentStatus = current,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

    private void RaiseUpdated(IReadOnlyList<string> changedFields, UserId? changedBy) =>
        RaiseDomainEvent(new JobPostingUpdatedDomainEvent
        {
            PostingId = Id,
            EmployerId = EmployerId,
            ChangedBy = changedBy,
            ChangedFields = changedFields
        });

    private static void Apply(
        bool changed, string field, Action mutate, List<string> changedFields)
    {
        if (!changed)
        {
            return;
        }

        mutate();
        changedFields.Add(field);
    }

    /// <summary>
    /// The most specific reason a transition was refused. A caller told only "invalid
    /// transition" cannot tell a filled posting from an archived one.
    /// </summary>
    private Error ErrorForRefusedTransition(JobStatus target) => Status switch
    {
        JobStatus.Filled => JobErrors.PostingFilled,
        JobStatus.Archived when target != JobStatus.Draft && target != JobStatus.Open =>
            JobErrors.PostingArchivedImmutable,
        JobStatus.Expired => JobErrors.PostingExpired,
        JobStatus.Draft => JobErrors.PostingNotPublished,
        _ => JobErrors.PostingNotOpen
    };

    private static Error LimitErrorFor(JobDetailKind kind) => kind switch
    {
        JobDetailKind.Responsibility => JobErrors.ResponsibilityLimit,
        JobDetailKind.Requirement => JobErrors.RequirementLimit,
        _ => JobErrors.BenefitLimit
    };

    private static Error InvalidErrorFor(JobDetailKind kind) => kind switch
    {
        JobDetailKind.Responsibility => JobErrors.ResponsibilityInvalid,
        JobDetailKind.Requirement => JobErrors.RequirementInvalid,
        _ => JobErrors.BenefitInvalid
    };

    private static string? NormalizeCategoryKey(string? categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        var key = categoryKey.Trim().ToLowerInvariant();

        var valid = key.Length <= JobLimits.MaxCategoryKey
            && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !key.StartsWith('-')
            && !key.EndsWith('-')
            && !key.Contains("--", StringComparison.Ordinal);

        return valid ? key : null;
    }
}
