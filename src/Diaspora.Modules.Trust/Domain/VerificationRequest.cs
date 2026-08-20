using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// A member's claim to be something, plus the evidence for it and the record of who judged it.
/// <para>
/// This is the aggregate the whole module turns on. It owns its documents and its reviews,
/// and it is the only thing permitted to move its own status — every transition is checked
/// against <see cref="VerificationWorkflow"/> first, so an illegal move is impossible rather
/// than merely unlikely.
/// </para>
/// <para>
/// It deliberately holds no reference to Identity. When a decision needs to reach a member's
/// access state it is raised as a domain event and translated at the edge into the published
/// <c>ApplyTrustDecisionRequest</c> contract — which, per ADR-002, is the only channel
/// through which a verification outcome may reach the credential store.
/// </para>
/// </summary>
public sealed class VerificationRequest : AggregateRoot<VerificationRequestId>, IAuditableEntity
{
    public const int MaxCountryCodeLength = 2;
    public const int MaxSupportingNotesLength = 2000;
    public const int MaxRejectionDetailsLength = 1000;
    public const int MaxExpertiseAreaLength = 80;
    public const int MaxExpertiseAreas = 10;
    public const int MaxSubjectTypeLength = 64;
    public const int MaxRequestedByLength = 64;
    public const int MaxSubmissionMetadataLength = 2000;

    private readonly List<VerificationDocument> _documents = [];
    private readonly List<VerificationReview> _reviews = [];
    private readonly List<string> _expertiseAreas = [];

    public UserId UserId { get; private set; } = default!;

    public VerificationType Type { get; private set; }

    public VerificationStatus Status { get; private set; }

    /// <summary>ISO-3166-1 alpha-2. The member's declared nationality.</summary>
    public string? Nationality { get; private set; }

    /// <summary>ISO-3166-1 alpha-2. Where the member says they now live.</summary>
    public string? ResidenceCountry { get; private set; }

    /// <summary>
    /// For a country/residency claim, which community it is about. Distinct from
    /// <see cref="ResidenceCountry"/> because a member may verify residency in a country
    /// they have since left.
    /// </summary>
    public string? TargetCountry { get; private set; }

    public DateOnly? ArrivalDate { get; private set; }

    public string? SupportingNotes { get; private set; }

    /// <summary>
    /// What the claim is about, when it is not the member themselves — <c>Provider</c>,
    /// <c>Organization</c>, a name a future vertical owns.
    /// <para>
    /// <b>Null means the member is the subject</b>, which is what every request opened before
    /// B3.0.2 meant and what an HTTP submission still means. The distinction matters for exactly
    /// one reason and it is not cosmetic: a member who runs two businesses submitting a
    /// <c>Business</c> verification for each is making two claims, not one twice, and without a
    /// subject the second is indistinguishable from a duplicate of the first.
    /// </para>
    /// </summary>
    public string? SubjectType { get; private set; }

    /// <summary>The subject aggregate's identifier in the owning module. Null for a member.</summary>
    public Guid? SubjectId { get; private set; }

    /// <summary>
    /// The submitting module's own identifier for the claim, and the idempotency key.
    /// <para>
    /// Unique where present, enforced by a filtered index. A retry carrying the same value
    /// finds this request rather than opening another.
    /// </para>
    /// </summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>Which module raised the claim. Null for a member's own HTTP submission.</summary>
    public string? RequestedBy { get; private set; }

    /// <summary>
    /// What the submitting module wanted the reviewer to see and Trust has no field for.
    /// <para>
    /// Stored and displayed, never parsed. Trust branching on a key here would be Trust learning
    /// about a vertical.
    /// </para>
    /// </summary>
    public string? SubmissionMetadata { get; private set; }

    /// <summary>Storage key for the biometric selfie, when the type requires a face match.</summary>
    public string? SelfieStorageKey { get; private set; }

    public VerificationRejectionReason? RejectionReason { get; private set; }

    public string? RejectionDetails { get; private set; }

    /// <summary>When a granted verification stops counting. Null means it does not lapse.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>
    /// Who currently holds this for review. Null means it is unassigned and anyone with the
    /// review permission may take it.
    /// </summary>
    public UserId? AssignedReviewerId { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    /// <summary>When review should have finished. Drives the overdue queue.</summary>
    public DateTimeOffset? ReviewDueAt { get; private set; }

    public IReadOnlyList<VerificationDocument> Documents => _documents.AsReadOnly();

    public IReadOnlyList<VerificationReview> Reviews => _reviews.AsReadOnly();

    /// <summary>Country-knowledge areas claimed alongside a residency verification.</summary>
    public IReadOnlyList<string> ExpertiseAreas => _expertiseAreas.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private VerificationRequest() { }

    private VerificationRequest(
        VerificationRequestId id,
        UserId userId,
        VerificationType type,
        DateTimeOffset submittedAt) : base(id)
    {
        UserId = userId;
        Type = type;
        Status = VerificationStatus.Submitted;
        SubmittedAt = submittedAt;
    }

    /// <summary>
    /// Opens a new request. It starts <see cref="VerificationStatus.Submitted"/> and does not
    /// enter the queue until <see cref="Queue"/> is called — which is what lets a handler
    /// attach documents first and still present one atomic "submitted" event to the outside.
    /// </summary>
    public static VerificationRequest Open(UserId userId, VerificationType type, DateTimeOffset submittedAt)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new VerificationRequest(VerificationRequestId.New(), userId, type, submittedAt);
    }

    /// <summary>
    /// Records what the claim is about and who raised it, for a submission that came from
    /// another module rather than from the member's own screen.
    /// <para>
    /// Additive in every sense: a request that never calls this is exactly the request B3
    /// opened, with the same duplicate semantics and the same review path. The subject changes
    /// nothing about how the claim is judged — only about which claims are the same claim.
    /// </para>
    /// </summary>
    public VerificationRequest WithSubject(
        string? subjectType,
        Guid? subjectId,
        Guid? correlationId = null,
        string? requestedBy = null,
        string? metadata = null)
    {
        SubjectType = Truncate(subjectType, MaxSubjectTypeLength);
        SubjectId = subjectId;
        CorrelationId = correlationId;
        RequestedBy = Truncate(requestedBy, MaxRequestedByLength);
        SubmissionMetadata = Truncate(metadata, MaxSubmissionMetadataLength);
        return this;
    }

    /// <summary>Records the nationality/residence pair a diaspora claim turns on.</summary>
    public VerificationRequest WithDiasporaClaim(
        string nationality,
        string residenceCountry,
        string? supportingNotes)
    {
        Nationality = NormalizeCountry(
            Guard.Against.NullOrWhiteSpace(nationality, nameof(nationality)));
        ResidenceCountry = NormalizeCountry(
            Guard.Against.NullOrWhiteSpace(residenceCountry, nameof(residenceCountry)));
        SupportingNotes = Truncate(supportingNotes, MaxSupportingNotesLength);
        return this;
    }

    /// <summary>Records the country and arrival date a residency claim turns on.</summary>
    public VerificationRequest WithResidencyClaim(
        string targetCountry,
        DateOnly? arrivalDate,
        IEnumerable<string>? expertiseAreas)
    {
        TargetCountry = NormalizeCountry(
            Guard.Against.NullOrWhiteSpace(targetCountry, nameof(targetCountry)));
        ArrivalDate = arrivalDate;

        _expertiseAreas.Clear();
        foreach (var area in expertiseAreas ?? [])
        {
            if (_expertiseAreas.Count >= MaxExpertiseAreas)
            {
                break;
            }

            var normalized = Truncate(area, MaxExpertiseAreaLength);
            if (normalized is not null && !_expertiseAreas.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                _expertiseAreas.Add(normalized);
            }
        }

        return this;
    }

    public VerificationRequest WithSelfie(string? selfieStorageKey)
    {
        SelfieStorageKey = Truncate(selfieStorageKey, VerificationDocument.MaxStorageKeyLength);
        return this;
    }

    /// <summary>
    /// Attaches evidence.
    /// <para>
    /// Re-attaching the same document type supersedes the previous one and bumps the version
    /// rather than replacing it, so a reviewer can still see what was originally sent. Only
    /// possible while the request is open — evidence cannot be added to a decided request,
    /// which is what stops a member quietly improving a rejected submission after the fact.
    /// </para>
    /// </summary>
    public VerificationDocument AttachDocument(
        VerificationDocumentType documentType,
        string storageKey,
        DateTimeOffset uploadedAt)
    {
        if (!VerificationWorkflow.IsOpen(Status))
        {
            throw new DomainException(
                $"Evidence cannot be attached to a verification in status '{Status}'.");
        }

        var superseded = _documents
            .Where(document => document.DocumentType == documentType
                && document.Status is VerificationDocumentStatus.Pending
                    or VerificationDocumentStatus.Rejected)
            .ToList();

        foreach (var document in superseded)
        {
            document.Supersede();
        }

        var version = _documents.Count(document => document.DocumentType == documentType) + 1;

        var attached = VerificationDocument.Attach(Id, documentType, storageKey, version, uploadedAt);
        _documents.Add(attached);
        return attached;
    }

    /// <summary>
    /// Attaches a Media asset as evidence.
    /// <para>
    /// <b>Appends rather than supersedes</b>, which is the one way it differs from
    /// <see cref="AttachDocument"/> and the difference is not incidental. Superseding is right
    /// for the member's wizard, where re-uploading "passport" means "use this one instead". It
    /// is wrong for a module submitting a set: three photographs of a shopfront are three pieces
    /// of evidence, and a rule that kept only the last would silently discard two of them and
    /// leave a reviewer wondering why the claim looked thin.
    /// </para>
    /// <para>
    /// The submitting module supplies the document type, because it is the only party that knows
    /// what the asset shows. Trust has not opened the file, and a type inferred from a Media
    /// identifier would be Trust inventing a fact about evidence it has not looked at — which
    /// matters, because the verification type's handler accepts some document types and refuses
    /// others.
    /// </para>
    /// </summary>
    public VerificationDocument AttachMediaEvidence(
        Guid mediaId,
        VerificationDocumentType documentType,
        DateTimeOffset uploadedAt)
    {
        if (!VerificationWorkflow.IsOpen(Status))
        {
            throw new DomainException(
                $"Evidence cannot be attached to a verification in status '{Status}'.");
        }

        var existing = _documents.FirstOrDefault(document => document.MediaId == mediaId);

        if (existing is not null)
        {
            return existing;
        }

        var version = _documents.Count(document => document.DocumentType == documentType) + 1;

        var attached = VerificationDocument
            .Attach(
                Id,
                documentType,
                VerificationDocument.MediaStorageKey(mediaId),
                version,
                uploadedAt)
            .WithMediaReference(mediaId);

        _documents.Add(attached);
        return attached;
    }

    /// <summary>
    /// Hands the request to review, optionally with a service-level deadline.
    /// <para>
    /// Raises <see cref="VerificationSubmittedDomainEvent"/> — the signal that eventually
    /// becomes both the outbound integration event and the <c>UnderReview</c> trust decision
    /// Identity is told about.
    /// </para>
    /// </summary>
    public void Queue(DateTimeOffset occurredAt, TimeSpan? reviewSla = null)
    {
        Transition(VerificationStatus.Pending, occurredAt);

        ReviewDueAt = reviewSla is { } sla ? occurredAt.Add(sla) : null;

        RaiseDomainEvent(new VerificationSubmittedDomainEvent(
            Id, UserId, Type, Nationality, ResidenceCountry, TargetCountry, occurredAt));
    }

    /// <summary>Takes the request onto a reviewer's desk.</summary>
    public void Assign(UserId reviewerId, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        if (Status is not (VerificationStatus.Pending or VerificationStatus.UnderReview))
        {
            throw new DomainException(
                $"A verification in status '{Status}' cannot be assigned for review.");
        }

        AssignedReviewerId = reviewerId;
        AssignedAt = occurredAt;

        if (Status == VerificationStatus.Pending)
        {
            Transition(VerificationStatus.UnderReview, occurredAt);
        }
    }

    /// <summary>Puts an assigned request back in the queue.</summary>
    public void Release(DateTimeOffset occurredAt)
    {
        if (Status != VerificationStatus.UnderReview)
        {
            throw new DomainException($"A verification in status '{Status}' is not assigned.");
        }

        AssignedReviewerId = null;
        AssignedAt = null;
        Transition(VerificationStatus.Pending, occurredAt);
    }

    /// <summary>
    /// Grants the verification.
    /// <para>
    /// The reviewer may not be the member — a self-review is refused here rather than only in
    /// the handler, because this is the invariant that makes the whole trust ladder mean
    /// anything.
    /// </para>
    /// </summary>
    public void Approve(
        UserId reviewerId,
        string? reviewerName,
        string? notes,
        DateTimeOffset occurredAt,
        DateTimeOffset? expiresAt = null,
        bool partial = false)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);
        GuardNotSelfReview(reviewerId);

        var target = partial ? VerificationStatus.PartiallyApproved : VerificationStatus.Approved;
        var from = Status;

        Transition(target, occurredAt);

        ExpiresAt = expiresAt;
        ReviewedAt = occurredAt;
        RejectionReason = null;
        RejectionDetails = null;

        AcceptPendingDocuments(notes, occurredAt);

        _reviews.Add(VerificationReview.ByReviewer(
            Id, reviewerId, reviewerName, from, target, null, notes, occurredAt));

        RaiseDomainEvent(new VerificationApprovedDomainEvent(
            Id, UserId, Type, TargetCountry, partial, expiresAt, occurredAt));
    }

    /// <summary>Grants the verification without a human, on a named rule's authority.</summary>
    public void AutoApprove(string ruleName, string? notes, DateTimeOffset occurredAt, DateTimeOffset? expiresAt = null)
    {
        var from = Status;
        Transition(VerificationStatus.Approved, occurredAt);

        ExpiresAt = expiresAt;
        ReviewedAt = occurredAt;

        AcceptPendingDocuments(notes, occurredAt);

        _reviews.Add(VerificationReview.ByRule(
            Id, ruleName, from, VerificationStatus.Approved,
            VerificationDecisionMode.AutomaticApproval, null, notes, occurredAt));

        RaiseDomainEvent(new VerificationApprovedDomainEvent(
            Id, UserId, Type, TargetCountry, false, expiresAt, occurredAt));
    }

    /// <summary>Refuses the verification. A machine-readable reason is mandatory.</summary>
    public void Reject(
        UserId reviewerId,
        string? reviewerName,
        VerificationRejectionReason reason,
        string? details,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);
        GuardNotSelfReview(reviewerId);

        var from = Status;
        Transition(VerificationStatus.Rejected, occurredAt);

        RejectionReason = reason;
        RejectionDetails = Truncate(details, MaxRejectionDetailsLength);
        ReviewedAt = occurredAt;

        _reviews.Add(VerificationReview.ByReviewer(
            Id, reviewerId, reviewerName, from, VerificationStatus.Rejected, reason, details, occurredAt));

        RaiseDomainEvent(new VerificationRejectedDomainEvent(
            Id, UserId, Type, TargetCountry, reason, RejectionDetails, occurredAt));
    }

    /// <summary>Refuses the verification without a human, on a named rule's authority.</summary>
    public void AutoReject(
        string ruleName,
        VerificationRejectionReason reason,
        string? details,
        DateTimeOffset occurredAt)
    {
        var from = Status;
        Transition(VerificationStatus.Rejected, occurredAt);

        RejectionReason = reason;
        RejectionDetails = Truncate(details, MaxRejectionDetailsLength);
        ReviewedAt = occurredAt;

        _reviews.Add(VerificationReview.ByRule(
            Id, ruleName, from, VerificationStatus.Rejected,
            VerificationDecisionMode.AutomaticRejection, reason, details, occurredAt));

        RaiseDomainEvent(new VerificationRejectedDomainEvent(
            Id, UserId, Type, TargetCountry, reason, RejectionDetails, occurredAt));
    }

    /// <summary>Asks the member for more evidence, naming what is missing.</summary>
    public void RequestMoreInformation(
        UserId reviewerId,
        string? reviewerName,
        string details,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);
        GuardNotSelfReview(reviewerId);

        var from = Status;
        Transition(VerificationStatus.NeedMoreInfo, occurredAt);

        RejectionReason = VerificationRejectionReason.ADDITIONAL_DOCS_NEEDED;
        RejectionDetails = Truncate(
            Guard.Against.NullOrWhiteSpace(details, nameof(details)), MaxRejectionDetailsLength);

        _reviews.Add(VerificationReview.ByReviewer(
            Id, reviewerId, reviewerName, from, VerificationStatus.NeedMoreInfo,
            VerificationRejectionReason.ADDITIONAL_DOCS_NEEDED, details, occurredAt));

        RaiseDomainEvent(new VerificationMoreInformationRequestedDomainEvent(
            Id, UserId, Type, RejectionDetails!, occurredAt));
    }

    /// <summary>Escalates beyond routine review.</summary>
    public void Escalate(UserId reviewerId, string? reviewerName, string? notes, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        var from = Status;
        Transition(VerificationStatus.UnderInvestigation, occurredAt);

        _reviews.Add(VerificationReview.ByReviewer(
            Id, reviewerId, reviewerName, from, VerificationStatus.UnderInvestigation,
            null, notes, occurredAt));
    }

    /// <summary>Marks a granted verification as lapsed.</summary>
    public void Expire(DateTimeOffset occurredAt)
    {
        var from = Status;
        Transition(VerificationStatus.Expired, occurredAt);

        foreach (var document in _documents.Where(d => d.Status == VerificationDocumentStatus.Accepted))
        {
            document.MarkExpired();
        }

        _reviews.Add(VerificationReview.ByRule(
            Id, "verification-expiry", from, VerificationStatus.Expired,
            VerificationDecisionMode.AutomaticRejection, VerificationRejectionReason.EXPIRED_DOCUMENT,
            "Verification lapsed.", occurredAt));

        RaiseDomainEvent(new VerificationExpiredDomainEvent(
            Id, UserId, Type, TargetCountry, occurredAt));
    }

    /// <summary>Withdraws a granted verification for cause.</summary>
    public void Revoke(UserId revokedBy, string? revokedByName, string reason, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(revokedBy);

        var from = Status;
        Transition(VerificationStatus.Revoked, occurredAt);

        RejectionDetails = Truncate(
            Guard.Against.NullOrWhiteSpace(reason, nameof(reason)), MaxRejectionDetailsLength);
        ReviewedAt = occurredAt;

        _reviews.Add(VerificationReview.ByReviewer(
            Id, revokedBy, revokedByName, from, VerificationStatus.Revoked, null, reason, occurredAt));

        RaiseDomainEvent(new VerificationRevokedDomainEvent(
            Id, UserId, Type, TargetCountry, RejectionDetails!, occurredAt));
    }

    /// <summary>Demands fresh evidence for a still-granted verification.</summary>
    public void RequireReverification(string reason, DateTimeOffset occurredAt)
    {
        var from = Status;
        Transition(VerificationStatus.ReverificationRequired, occurredAt);

        RejectionDetails = Truncate(reason, MaxRejectionDetailsLength);

        _reviews.Add(VerificationReview.ByRule(
            Id, "reverification-policy", from, VerificationStatus.ReverificationRequired,
            VerificationDecisionMode.RoutedToReview, null, reason, occurredAt));
    }

    /// <summary>Marks the request as under appeal. Called by the <see cref="Appeal"/> flow.</summary>
    public void MarkAppealed(DateTimeOffset occurredAt)
    {
        if (!VerificationWorkflow.IsAppealable(Status))
        {
            throw new DomainException(
                $"A verification in status '{Status}' cannot be appealed.");
        }

        Transition(VerificationStatus.Appealed, occurredAt);
    }

    /// <summary>
    /// Applies an appeal's outcome. Upheld appeals grant; dismissed appeals restore the
    /// original refusal, so a failed appeal leaves no trace on the member's standing beyond
    /// the appeal record itself.
    /// </summary>
    public void ResolveAppeal(bool upheld, string? notes, DateTimeOffset occurredAt)
    {
        if (Status != VerificationStatus.Appealed)
        {
            throw new DomainException(
                $"A verification in status '{Status}' has no appeal to resolve.");
        }

        var target = upheld ? VerificationStatus.Approved : VerificationStatus.Rejected;
        Transition(target, occurredAt);

        ReviewedAt = occurredAt;

        if (upheld)
        {
            RejectionReason = null;
            RejectionDetails = null;
            AcceptPendingDocuments(notes, occurredAt);

            RaiseDomainEvent(new VerificationApprovedDomainEvent(
                Id, UserId, Type, TargetCountry, false, ExpiresAt, occurredAt));
        }

        _reviews.Add(VerificationReview.ByRule(
            Id, "appeal-outcome", VerificationStatus.Appealed, target,
            VerificationDecisionMode.ManualReview, RejectionReason, notes, occurredAt));
    }

    /// <summary>Reopens a decided request so the member can try again.</summary>
    public void Reopen(DateTimeOffset occurredAt)
    {
        Transition(VerificationStatus.Pending, occurredAt);

        RejectionReason = null;
        RejectionDetails = null;
        ReviewedAt = null;
        AssignedReviewerId = null;
        AssignedAt = null;
    }

    /// <summary>Whether the member currently holds this verification.</summary>
    public bool IsGranted(DateTimeOffset asOf) =>
        VerificationWorkflow.IsGrant(Status) && (ExpiresAt is null || ExpiresAt > asOf);

    /// <summary>Whether review has run past its deadline.</summary>
    public bool IsOverdue(DateTimeOffset asOf) =>
        VerificationWorkflow.IsOpen(Status) && ReviewDueAt is { } due && due < asOf;

    private void Transition(VerificationStatus to, DateTimeOffset occurredAt)
    {
        if (Status == to)
        {
            return;
        }

        if (!VerificationWorkflow.CanTransition(Status, to))
        {
            throw new DomainException(
                $"A verification cannot move from '{Status}' to '{to}'.");
        }

        var from = Status;
        Status = to;
        ModifiedAt = occurredAt;

        RaiseDomainEvent(new VerificationStatusChangedDomainEvent(Id, UserId, Type, from, to, occurredAt));
    }

    private void GuardNotSelfReview(UserId reviewerId)
    {
        if (reviewerId == UserId)
        {
            throw new DomainException("A member cannot review their own verification.");
        }
    }

    private void AcceptPendingDocuments(string? notes, DateTimeOffset occurredAt)
    {
        foreach (var document in _documents.Where(d => d.Status == VerificationDocumentStatus.Pending))
        {
            document.Accept(notes, occurredAt);
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string NormalizeCountry(string value)
    {
        var trimmed = value.Trim().ToUpperInvariant();
        return trimmed.Length <= MaxCountryCodeLength ? trimmed : trimmed[..MaxCountryCodeLength];
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
