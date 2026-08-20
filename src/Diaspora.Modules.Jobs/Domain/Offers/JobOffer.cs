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

namespace Diaspora.Modules.Jobs.Domain.Offers;

/// <summary>
/// Terms an employer put in front of a candidate.
/// <para>
/// <b>The most consequential record in the module, and the one that most needs to be
/// unambiguous.</b> A candidate is being asked to accept a job — very often to move country for
/// it — and everything they need to decide has to be written down, dated, and impossible to
/// quietly change afterwards. That is why the salary is required where a posting's is optional,
/// why the response deadline is enforced by the clock rather than by anyone remembering, and
/// why revising terms produces a new offer rather than an edit.
/// </para>
/// <para>
/// <b><see cref="OfferStatus.Draft"/> is real and useful.</b> Terms usually need an internal
/// approval before they go out, and nothing is visible to the candidate until
/// <see cref="Extend"/>. That also gives an employer somewhere safe to get the numbers wrong.
/// </para>
/// <para>
/// <b>Expiry is computed, never stored as a flag.</b> <see cref="StatusAsOf"/> takes the
/// current instant, so an offer whose deadline passed is expired the moment it passed — not
/// whenever a sweep next ran. The sweep exists to fire the notification, not to make the fact
/// true.
/// </para>
/// <para>
/// The platform computes no tax and no take-home pay: international tax calculation is
/// explicitly out of scope, and guessing would be advice this module cannot stand behind.
/// </para>
/// </summary>
public sealed class JobOffer
    : AggregateRoot<JobOfferId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private JobOffer(
        JobOfferId id,
        JobApplicationId applicationId,
        JobPostingId postingId,
        EmployerProfileId employerId,
        CandidateProfileId candidateId,
        OfferTerms terms,
        DateTimeOffset createdAt)
        : base(id)
    {
        ApplicationId = applicationId;
        PostingId = postingId;
        EmployerId = employerId;
        CandidateId = candidateId;
        Terms = terms;
        Status = OfferStatus.Draft;
        CreatedAt = createdAt;
    }

    private JobOffer() { }

    // ── Parties ─────────────────────────────────────────────────────────────────────────────

    public JobApplicationId ApplicationId { get; private set; } = default!;

    /// <summary>Denormalised, so filling a vacancy needs no join to find the posting.</summary>
    public JobPostingId PostingId { get; private set; } = default!;

    /// <summary>Denormalised, so the ownership check is a column comparison.</summary>
    public EmployerProfileId EmployerId { get; private set; } = default!;

    public CandidateProfileId CandidateId { get; private set; } = default!;

    /// <summary>
    /// The platform's ownership seam. The employer owns the offer — the candidate answers it
    /// but does not own it, and the distinction is what stops a candidate editing their own
    /// terms.
    /// </summary>
    public Guid OwnerId => EmployerId.Value;

    // ── The offer ───────────────────────────────────────────────────────────────────────────

    public OfferTerms Terms { get; private set; } = default!;

    /// <summary>
    /// The stored status. Read <see cref="StatusAsOf"/> instead wherever an answer must account
    /// for the clock — a live offer past its deadline is expired whether or not a sweep ran.
    /// </summary>
    public OfferStatus Status { get; private set; }

    public DateTimeOffset? ExtendedAt { get; private set; }

    public DateTimeOffset? AnsweredAt { get; private set; }

    /// <summary>Who answered. Null on an expiry, which nobody decided.</summary>
    public UserId? AnsweredBy { get; private set; }

    /// <summary>Why it ended as it did. The candidate's or the employer's words.</summary>
    public string? ResponseReason { get; private set; }

    /// <summary>
    /// The offer this one supersedes, when the employer revised terms. Revising produces a new
    /// offer rather than editing this one, so the candidate can always see what they were
    /// originally told.
    /// </summary>
    public JobOfferId? SupersedesOfferId { get; private set; }

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

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
    /// The status accounting for the clock. An offer whose deadline has passed reads as
    /// <see cref="OfferStatus.Expired"/> here even before the sweep has written it down.
    /// </summary>
    public OfferStatus StatusAsOf(DateTimeOffset now) =>
        Status == OfferStatus.Extended && Terms.HasExpired(now)
            ? OfferStatus.Expired
            : Status;

    /// <summary>Whether the candidate can still answer.</summary>
    public bool IsAwaitingResponse(DateTimeOffset now) =>
        !IsDeleted && StatusAsOf(now) == OfferStatus.Extended;

    /// <summary>Whether the candidate can see it at all.</summary>
    public bool IsVisibleToCandidate => Status != OfferStatus.Draft;

    /// <summary>How long the candidate has left. Null when it is not theirs to answer.</summary>
    public TimeSpan? TimeRemaining(DateTimeOffset now) =>
        IsAwaitingResponse(now) ? Terms.ExpiresAt - now : null;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drafts an offer. Nothing is visible to the candidate until <see cref="Extend"/>.
    /// </summary>
    public static Result<JobOffer> Draft(
        JobOfferId id,
        JobApplicationId applicationId,
        JobPostingId postingId,
        EmployerProfileId employerId,
        CandidateProfileId candidateId,
        OfferTerms terms,
        DateTimeOffset now,
        JobOfferId? supersedesOfferId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(applicationId);
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(employerId);
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(terms);

        return Result.Success(new JobOffer(
            id, applicationId, postingId, employerId, candidateId, terms, now)
        {
            SupersedesOfferId = supersedesOfferId
        });
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Employer actions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Puts the terms in front of the candidate. The moment the offer becomes real.</summary>
    public Result Extend(DateTimeOffset now, UserId? extendedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.OfferNotFound);
        }

        if (Status == OfferStatus.Extended)
        {
            return Result.Failure(JobErrors.OfferAlreadyExtended);
        }

        if (Status != OfferStatus.Draft)
        {
            return Result.Failure(JobErrors.OfferAlreadyDecided);
        }

        if (Terms.HasExpired(now))
        {
            return Result.Failure(JobErrors.OfferExpiryInPast);
        }

        Status = OfferStatus.Extended;
        ExtendedAt = now;
        Touch();

        RaiseDomainEvent(new OfferExtendedDomainEvent
        {
            OfferId = Id,
            ApplicationId = ApplicationId,
            PostingId = PostingId,
            EmployerId = EmployerId,
            CandidateId = CandidateId,
            SalaryAmount = Terms.SalaryAmount,
            CurrencyCode = Terms.CurrencyCode,
            Period = Terms.Period,
            StartDate = Terms.StartDate,
            ExpiresAt = Terms.ExpiresAt,
            ExtendedBy = extendedBy,
            ExtendedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Amends a draft's terms. Only a draft — once terms are with a candidate they are a
    /// statement that was made, and revising them means issuing a new offer that supersedes
    /// this one.
    /// </summary>
    public Result AmendTerms(OfferTerms terms, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.OfferNotFound);
        }

        if (Status != OfferStatus.Draft)
        {
            return Result.Failure(JobErrors.OfferAlreadyExtended);
        }

        var revision = EnsureRevision(baseRevision);

        if (revision.IsFailure)
        {
            return revision;
        }

        Terms = terms;
        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Gives the candidate longer to answer.
    /// <para>
    /// Its own operation rather than an amendment, because it is the one change to a live offer
    /// that can only help the candidate — and forcing an employer to withdraw and re-issue in
    /// order to grant an extra week would be a rule that punishes the considerate.
    /// </para>
    /// </summary>
    public Result ExtendDeadline(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.OfferNotFound);
        }

        if (StatusAsOf(now) is not (OfferStatus.Extended or OfferStatus.Expired))
        {
            return Result.Failure(JobErrors.OfferNotExtended);
        }

        if (Status is OfferStatus.Accepted or OfferStatus.Rejected or OfferStatus.Withdrawn)
        {
            return Result.Failure(JobErrors.OfferAlreadyDecided);
        }

        var amended = Terms.WithDeadline(expiresAt, now);

        if (amended.IsFailure)
        {
            return Result.Failure(amended.Error);
        }

        Terms = amended.Value;

        // An offer brought back from expiry is live again — which is exactly what an employer
        // granting more time means, and the candidate is told through the notification.
        Status = OfferStatus.Extended;
        Touch();

        return Result.Success();
    }

    /// <summary>Pulls the offer before the candidate answered.</summary>
    public Result Withdraw(string? reason, DateTimeOffset now, UserId? withdrawnBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.OfferNotFound);
        }

        if (Status is OfferStatus.Accepted or OfferStatus.Rejected)
        {
            return Result.Failure(JobErrors.OfferAlreadyDecided);
        }

        if (Status == OfferStatus.Withdrawn)
        {
            return Result.Success();
        }

        return Answer(OfferStatus.Withdrawn, withdrawnBy, reason, now);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Candidate actions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The candidate accepts. The only path in the module that fills a vacancy, and it does so
    /// through the event rather than by reaching into the posting aggregate.
    /// </summary>
    public Result Accept(DateTimeOffset now, UserId acceptedBy, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedBy);

        var guard = EnsureAnswerable(now);

        if (guard.IsFailure)
        {
            return guard;
        }

        return Answer(OfferStatus.Accepted, acceptedBy, note, now);
    }

    /// <summary>The candidate declines. The reason is always optional.</summary>
    public Result Reject(DateTimeOffset now, UserId rejectedBy, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(rejectedBy);

        var guard = EnsureAnswerable(now);

        if (guard.IsFailure)
        {
            return guard;
        }

        return Answer(OfferStatus.Rejected, rejectedBy, reason, now);
    }

    /// <summary>
    /// Writes down that the deadline passed. The sweep's entry point.
    /// <para>
    /// A no-op unless the offer really has lapsed, so the sweep can call it speculatively over
    /// a batch without checking each one first.
    /// </para>
    /// </summary>
    public Result Expire(DateTimeOffset now)
    {
        if (IsDeleted || Status != OfferStatus.Extended || !Terms.HasExpired(now))
        {
            return Result.Success();
        }

        return Answer(OfferStatus.Expired, answeredBy: null, reason: null, now);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency, soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save based on a revision the caller no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
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

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private void Touch() => Revision++;

    /// <summary>The shared guard for a candidate's answer, expiry included.</summary>
    private Result EnsureAnswerable(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.OfferNotFound);
        }

        if (Status == OfferStatus.Draft)
        {
            return Result.Failure(JobErrors.OfferNotExtended);
        }

        if (Status != OfferStatus.Extended)
        {
            return Result.Failure(JobErrors.OfferAlreadyDecided);
        }

        return Terms.HasExpired(now)
            ? Result.Failure(JobErrors.OfferExpired)
            : Result.Success();
    }

    private Result Answer(
        OfferStatus status, UserId? answeredBy, string? reason, DateTimeOffset now)
    {
        Status = status;
        AnsweredAt = now;
        AnsweredBy = answeredBy;
        ResponseReason = Clip(reason, JobLimits.MaxOfferNote);
        Touch();

        RaiseDomainEvent(new OfferAnsweredDomainEvent
        {
            OfferId = Id,
            ApplicationId = ApplicationId,
            PostingId = PostingId,
            EmployerId = EmployerId,
            CandidateId = CandidateId,
            Status = status,
            AnsweredBy = answeredBy,
            Reason = ResponseReason,
            AnsweredAt = now
        });

        return Result.Success();
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
