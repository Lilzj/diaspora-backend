using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Alerts;

/// <summary>
/// A standing search a candidate asked to be told about.
/// <para>
/// <b>The single most valuable thing this module does for a candidate.</b> Job hunting across
/// borders means the right posting appears at three in the morning in a country you are not in,
/// and is filled before you next open the app. An alert is the platform watching on their behalf.
/// </para>
/// <para>
/// <b><see cref="LastSweptAt"/> is the correctness mechanism, not a statistic.</b> A sweep asks
/// for postings published since it, so nothing is missed when a run is late and nothing is sent
/// twice when a run overlaps — the watermark advances only on a completed pass. Sweeping on
/// "the last day" instead would double-send every time a job ran early and skip every time one
/// failed.
/// </para>
/// <para>
/// <b>An alert is paused rather than deleted when it goes quiet.</b> A candidate who takes a job
/// wants their alerts to stop, not to be lost — they will be looking again in two years, and
/// the criteria they carefully built are worth keeping.
/// </para>
/// </summary>
public sealed class JobAlert : AggregateRoot<JobAlertId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private JobAlert(
        JobAlertId id,
        UserId userId,
        CandidateProfileId? candidateId,
        string name,
        JobAlertCriteria criteria,
        JobAlertFrequency frequency,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        CandidateId = candidateId;
        Name = name;
        Criteria = criteria;
        Frequency = frequency;
        IsActive = true;

        // The watermark starts at creation, so the first sweep reports what has been published
        // since the candidate asked — not the entire back catalogue, which would arrive as a
        // hundred-item digest and teach them to ignore the next one.
        LastSweptAt = createdAt;
        CreatedAt = createdAt;
    }

    private JobAlert() { }

    /// <summary>The member. An alert needs no candidate profile — watching is not applying.</summary>
    public UserId UserId { get; private set; } = default!;

    /// <summary>Their profile, when they have one.</summary>
    public CandidateProfileId? CandidateId { get; private set; }

    /// <summary>The platform's ownership seam.</summary>
    public Guid OwnerId => UserId.Value;

    /// <summary>What the candidate calls it. "Nursing, Germany, sponsored".</summary>
    public string Name { get; private set; } = string.Empty;

    public JobAlertCriteria Criteria { get; private set; } = default!;

    public JobAlertFrequency Frequency { get; private set; }

    /// <summary>Whether it is watching. Paused alerts keep their criteria.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The watermark. The next sweep asks for postings published after this, so a late run
    /// misses nothing and an overlapping run sends nothing twice.
    /// </summary>
    public DateTimeOffset LastSweptAt { get; private set; }

    /// <summary>When a digest last went out. Null until the first match.</summary>
    public DateTimeOffset? LastNotifiedAt { get; private set; }

    /// <summary>How many postings this alert has ever surfaced. The candidate's own stat.</summary>
    public int TotalMatchCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Whether this alert is due a sweep at the given instant.</summary>
    public bool IsDue(DateTimeOffset now)
    {
        if (IsDeleted || !IsActive)
        {
            return false;
        }

        var interval = Frequency switch
        {
            JobAlertFrequency.Immediate => TimeSpan.FromMinutes(15),
            JobAlertFrequency.Daily => TimeSpan.FromHours(24),
            _ => TimeSpan.FromDays(7)
        };

        return now - LastSweptAt >= interval;
    }

    public static Result<JobAlert> Create(
        JobAlertId id,
        UserId userId,
        CandidateProfileId? candidateId,
        string? name,
        JobAlertCriteria criteria,
        JobAlertFrequency frequency,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.IsEmpty)
        {
            return Result.Failure<JobAlert>(JobErrors.AlertCriteriaEmpty);
        }

        if (!Enum.IsDefined(frequency))
        {
            return Result.Failure<JobAlert>(JobErrors.AlertNameInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        if (trimmed is null || trimmed.Length > JobLimits.MaxAlertName)
        {
            return Result.Failure<JobAlert>(JobErrors.AlertNameInvalid);
        }

        return Result.Success(
            new JobAlert(id, userId, candidateId, trimmed, criteria, frequency, now));
    }

    /// <summary>Changes what the alert watches for and how often it reports.</summary>
    public Result Update(string? name, JobAlertCriteria criteria, JobAlertFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.AlertNotFound);
        }

        if (criteria.IsEmpty)
        {
            return Result.Failure(JobErrors.AlertCriteriaEmpty);
        }

        if (!Enum.IsDefined(frequency))
        {
            return Result.Failure(JobErrors.AlertNameInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        if (trimmed is null || trimmed.Length > JobLimits.MaxAlertName)
        {
            return Result.Failure(JobErrors.AlertNameInvalid);
        }

        Name = trimmed;
        Criteria = criteria;
        Frequency = frequency;

        return Result.Success();
    }

    /// <summary>Stops the alert watching without losing what it watched for.</summary>
    public void Pause() => IsActive = false;

    /// <summary>
    /// Starts it watching again, from now rather than from where it left off. A candidate
    /// returning after a year does not want twelve months of back catalogue in one digest.
    /// </summary>
    public void Resume(DateTimeOffset now)
    {
        IsActive = true;
        LastSweptAt = now;
    }

    /// <summary>
    /// Records a completed sweep and, when it found anything, raises the digest.
    /// <para>
    /// The watermark advances whether or not there were matches — that is what makes an empty
    /// pass cheap and a late pass correct. The event is raised only when there is something to
    /// say.
    /// </para>
    /// </summary>
    public Result RecordSweep(IReadOnlyList<Guid> matchedPostingIds, DateTimeOffset sweptAt)
    {
        ArgumentNullException.ThrowIfNull(matchedPostingIds);

        if (IsDeleted || !IsActive)
        {
            return Result.Success();
        }

        LastSweptAt = sweptAt;

        if (matchedPostingIds.Count == 0)
        {
            return Result.Success();
        }

        LastNotifiedAt = sweptAt;
        TotalMatchCount += matchedPostingIds.Count;

        RaiseDomainEvent(new JobAlertMatchedDomainEvent
        {
            AlertId = Id,
            UserId = UserId,
            AlertName = Name,
            Frequency = Frequency,
            PostingIds = matchedPostingIds,
            MatchedAt = sweptAt
        });

        return Result.Success();
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        IsActive = false;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
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
}
