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

namespace Diaspora.Modules.Jobs.Domain.Employers;

/// <summary>
/// An organisation that hires. The party a candidate is actually dealing with.
/// <para>
/// <b>An employer is a team, not a person.</b> Its members are held here with roles, and every
/// employer-scoped authority question in the module is answered by
/// <see cref="RoleOf"/> — not by a permission on a token, because a member may own one employer,
/// recruit at another and have nothing to do with a third. A recruiter who leaves takes nothing
/// with them, and the pipeline they were working stays with the organisation.
/// </para>
/// <para>
/// <b>Status and trust mark are separate, and conflating them would break something real.</b>
/// <see cref="Status"/> is permission to operate and is the platform's to withdraw for
/// misconduct. <see cref="TrustMark"/> is a badge and is Trust's alone. A verified employer can
/// be suspended; an unverified one can hire perfectly legitimately. If they were one field,
/// either misconduct would cost a badge Trust owns, or a new employer could not post until
/// Trust got round to them.
/// </para>
/// <para>
/// <b>What it owns:</b> its identity, contact details, branding, hiring regions, members and
/// its measured responsiveness. <b>What it does not:</b> its postings, which are their own
/// aggregate — an employer with three hundred open roles must not be loaded to change a phone
/// number.
/// </para>
/// </summary>
public sealed class EmployerProfile
    : AggregateRoot<EmployerProfileId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<EmployerMember> _members = [];
    private readonly List<EmployerHiringRegion> _hiringRegions = [];

    private EmployerProfile(
        EmployerProfileId id,
        UserId ownerUserId,
        CompanyIdentity identity,
        JobSlug slug,
        string primaryCountryCode,
        DateTimeOffset createdAt)
        : base(id)
    {
        OwnerUserId = ownerUserId;
        Identity = identity;
        Slug = slug;
        PrimaryCountryCode = primaryCountryCode;
        Contact = CompanyContact.Empty;
        Branding = EmployerBranding.Empty;
        TrustMark = JobTrustMark.None;
        Responsiveness = EmployerResponsiveness.None;
        Status = EmployerStatus.Active;
        CreatedAt = createdAt;

        _members.Add(EmployerMember.Create(ownerUserId, EmployerMemberRole.Owner, createdAt));
    }

    private EmployerProfile() { }

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The member who registered it. Convenience for attribution and the default owner; it is
    /// not the authority check — <see cref="RoleOf"/> is, because ownership can be transferred
    /// and this field would then be a lie.
    /// </summary>
    public UserId OwnerUserId { get; private set; } = default!;

    /// <summary>The platform's ownership seam. The employer answers for itself.</summary>
    public Guid OwnerId => Id.Value;

    /// <summary>
    /// The member a message to this employer is addressed to — the longest-standing owner.
    /// <para>
    /// <b>Not <see cref="OwnerUserId"/>, and the difference is the point.</b> That field records
    /// who registered the organisation and is never rewritten, so after an ownership transfer it
    /// names somebody who may no longer be an owner — which is fine for attribution and wrong
    /// for delivery. This reads the current membership, the same source
    /// <see cref="RoleOf"/> answers from.
    /// </para>
    /// <para>
    /// Falls back to the registering owner when the aggregate was loaded without its members. A
    /// stale recipient is a worse answer than a current one and a better answer than none.
    /// </para>
    /// </summary>
    public UserId NotifiableOwnerUserId =>
        _members
            .Where(member => member.Role == EmployerMemberRole.Owner)
            .OrderBy(member => member.JoinedAt)
            .Select(member => member.UserId)
            .FirstOrDefault()
        ?? OwnerUserId;

    public CompanyIdentity Identity { get; private set; } = default!;

    /// <summary>The deep-link handle. Allocated once and never recomputed.</summary>
    public JobSlug Slug { get; private set; } = default!;

    /// <summary>Where the organisation is based. Upper-case ISO 3166-1 alpha-2.</summary>
    public string PrimaryCountryCode { get; private set; } = string.Empty;

    /// <summary>What the organisation does. Free prose, rendered on its profile page.</summary>
    public string? Description { get; private set; }

    public CompanyContact Contact { get; private set; } = default!;

    public EmployerBranding Branding { get; private set; } = default!;

    // ── Standing ────────────────────────────────────────────────────────────────────────────

    /// <summary>Permission to operate. The platform's, and distinct from the badge.</summary>
    public EmployerStatus Status { get; private set; }

    /// <summary>The badge. Trust's, and distinct from permission to operate.</summary>
    public JobTrustMark TrustMark { get; private set; } = default!;

    /// <summary>How this employer actually treats applicants. Measured, not claimed.</summary>
    public EmployerResponsiveness Responsiveness { get; private set; } = default!;

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    public IReadOnlyList<EmployerMember> Members => _members.AsReadOnly();

    public IReadOnlyList<EmployerHiringRegion> HiringRegions =>
        _hiringRegions.OrderBy(region => region.Order).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether this employer may post and recruit right now.</summary>
    public bool CanRecruit => !IsDeleted && Status == EmployerStatus.Active;

    /// <summary>Whether it carries a Trust badge.</summary>
    public bool IsVerified => TrustMark.IsVerified;

    /// <summary>Whether candidates should be told they are dealing with an intermediary.</summary>
    public bool IsIntermediary => Identity.IsIntermediary;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers an employer, with its creator as the first owner.
    /// <para>
    /// The owner membership is created here rather than by the handler, because an employer with
    /// no owner is an aggregate nobody can ever act on — and the invariant "at least one owner"
    /// has to hold from the first instant, not from the first successful follow-up write.
    /// </para>
    /// </summary>
    public static Result<EmployerProfile> Create(
        EmployerProfileId id,
        UserId ownerUserId,
        CompanyIdentity identity,
        JobSlug slug,
        string? primaryCountryCode,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ownerUserId);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(slug);

        var country = EmployerHiringRegion.NormalizeCountry(primaryCountryCode);

        if (country is null)
        {
            return Result.Failure<EmployerProfile>(JobErrors.CountryInvalid);
        }

        var employer = new EmployerProfile(id, ownerUserId, identity, slug, country, now);

        employer.RaiseDomainEvent(new EmployerRegisteredDomainEvent
        {
            EmployerId = id,
            OwnerId = ownerUserId,
            DisplayName = identity.DisplayName,
            Slug = slug.Value,
            OrganizationType = identity.OrganizationType,
            CountryCode = country
        });

        return Result.Success(employer);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save based on a revision the editor no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Membership — the module's authorisation primitive
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// This member's role here, or null if they are not part of this employer. Every
    /// employer-scoped authority question in the module goes through this.
    /// </summary>
    public EmployerMemberRole? RoleOf(UserId userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return _members.Find(member => member.UserId == userId)?.Role;
    }

    /// <summary>Whether this member holds at least the authority asked for.</summary>
    public bool HasAtLeast(UserId userId, EmployerMemberRole required) =>
        RoleOf(userId) is { } role && role >= required;

    /// <summary>Adds a member, or changes the role of one already here. Idempotent on the pair.</summary>
    public Result AddMember(
        UserId userId, EmployerMemberRole role, DateTimeOffset now, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.EmployerNotFound);
        }

        if (!Enum.IsDefined(role))
        {
            return Result.Failure(JobErrors.RecruiterNotMember);
        }

        var existing = _members.Find(member => member.UserId == userId);

        if (existing is not null)
        {
            return existing.Role == role
                ? Result.Success()
                : ChangeMemberRole(userId, role, now, changedBy);
        }

        if (_members.Count >= JobLimits.MaxEmployerMembers)
        {
            return Result.Failure(JobErrors.RecruiterAlreadyMember);
        }

        _members.Add(EmployerMember.Create(userId, role, now));
        Touch();

        RaiseDomainEvent(new EmployerMembershipChangedDomainEvent
        {
            EmployerId = Id,
            MemberId = userId,
            PreviousRole = null,
            CurrentRole = role,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Changes a member's role.
    /// <para>
    /// Demoting the last owner is refused. An employer with no owner is an aggregate nobody can
    /// administer, delete or transfer — a state easy to reach by accident and impossible to
    /// leave without a database edit.
    /// </para>
    /// </summary>
    public Result ChangeMemberRole(
        UserId userId, EmployerMemberRole role, DateTimeOffset now, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var member = _members.Find(candidate => candidate.UserId == userId);

        if (member is null)
        {
            return Result.Failure(JobErrors.RecruiterNotMember);
        }

        if (!Enum.IsDefined(role))
        {
            return Result.Failure(JobErrors.RecruiterNotMember);
        }

        if (member.Role == role)
        {
            return Result.Success();
        }

        if (member.Role == EmployerMemberRole.Owner
            && role != EmployerMemberRole.Owner
            && OwnerCount == 1)
        {
            return Result.Failure(JobErrors.CannotRemoveLastOwner);
        }

        var previous = member.Role;
        member.ChangeRole(role);
        Touch();

        RaiseDomainEvent(new EmployerMembershipChangedDomainEvent
        {
            EmployerId = Id,
            MemberId = userId,
            PreviousRole = previous,
            CurrentRole = role,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>Removes a member. Refuses to remove the last owner, for the same reason.</summary>
    public Result RemoveMember(UserId userId, DateTimeOffset now, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var member = _members.Find(candidate => candidate.UserId == userId);

        if (member is null)
        {
            return Result.Failure(JobErrors.RecruiterNotMember);
        }

        if (member.Role == EmployerMemberRole.Owner && OwnerCount == 1)
        {
            return Result.Failure(JobErrors.CannotRemoveLastOwner);
        }

        var previous = member.Role;
        _members.Remove(member);
        Touch();

        RaiseDomainEvent(new EmployerMembershipChangedDomainEvent
        {
            EmployerId = Id,
            MemberId = userId,
            PreviousRole = previous,
            CurrentRole = null,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Details
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Updates everything an owner or administrator can change about the organisation.</summary>
    public Result UpdateProfile(
        CompanyIdentity identity,
        CompanyContact contact,
        EmployerBranding branding,
        string? description,
        string? primaryCountryCode,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(branding);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var country = EmployerHiringRegion.NormalizeCountry(primaryCountryCode);

        if (country is null)
        {
            return Result.Failure(JobErrors.CountryInvalid);
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (trimmedDescription is { Length: > JobLimits.MaxEmployerDescription })
        {
            return Result.Failure(JobErrors.EmployerNameInvalid);
        }

        var changed = new List<string>();

        if (!Identity.Equals(identity))
        {
            Identity = identity;
            changed.Add("identity");
        }

        if (!Contact.Equals(contact))
        {
            Contact = contact;
            changed.Add("contact");
        }

        if (!Branding.Equals(branding))
        {
            Branding = branding;
            changed.Add("branding");
        }

        if (!string.Equals(Description, trimmedDescription, StringComparison.Ordinal))
        {
            Description = trimmedDescription;
            changed.Add("description");
        }

        if (!string.Equals(PrimaryCountryCode, country, StringComparison.Ordinal))
        {
            PrimaryCountryCode = country;
            changed.Add("country");
        }

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();

        RaiseDomainEvent(new EmployerUpdatedDomainEvent
        {
            EmployerId = Id,
            ChangedBy = changedBy,
            ChangedFields = changed
        });

        return Result.Success();
    }

    /// <summary>Replaces the hiring regions wholesale. Deduplicated on the country code.</summary>
    public Result SetHiringRegions(
        IReadOnlyList<EmployerHiringRegionDraft> regions,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(regions);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (regions.Count > JobLimits.MaxHiringRegions)
        {
            return Result.Failure(JobErrors.EmployerRegionLimit);
        }

        var rebuilt = new List<EmployerHiringRegion>(regions.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in regions)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.EmployerRegionInvalid);
            }

            var country = EmployerHiringRegion.NormalizeCountry(draft.CountryCode);

            if (country is null)
            {
                return Result.Failure(JobErrors.EmployerRegionInvalid);
            }

            if (seen.Add(country))
            {
                rebuilt.Add(EmployerHiringRegion.Create(
                    country, draft.SponsorsVisas, rebuilt.Count));
            }
        }

        _hiringRegions.Clear();
        _hiringRegions.AddRange(rebuilt);

        Touch();

        RaiseDomainEvent(new EmployerUpdatedDomainEvent
        {
            EmployerId = Id,
            ChangedBy = changedBy,
            ChangedFields = ["regions"]
        });

        return Result.Success();
    }

    /// <summary>Whether this employer says it hires into a country.</summary>
    public bool HiresInto(string countryCode)
    {
        var country = EmployerHiringRegion.NormalizeCountry(countryCode);

        return country is not null
            && (_hiringRegions.Exists(region =>
                    string.Equals(region.CountryCode, country, StringComparison.Ordinal))
                || string.Equals(PrimaryCountryCode, country, StringComparison.Ordinal));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Status
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Bars the employer from posting and recruiting. Reversible.</summary>
    public Result Suspend(string? reason, UserId moderatorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > JobLimits.MaxModerationNote)
        {
            return Result.Failure(JobErrors.ModerationNoteInvalid);
        }

        return MoveTo(EmployerStatus.Suspended, now, moderatorId, trimmed);
    }

    /// <summary>Lifts a suspension. Idempotent.</summary>
    public Result Reinstate(UserId moderatorId, DateTimeOffset now) =>
        Status == EmployerStatus.Suspended
            ? MoveTo(EmployerStatus.Active, now, moderatorId, reason: null)
            : Result.Success();

    /// <summary>Closes the employer at its owner's request. Its postings are archived by a handler.</summary>
    public Result Close(UserId closedBy, DateTimeOffset now) =>
        MoveTo(EmployerStatus.Closed, now, closedBy, reason: null);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification — projection of Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records that a verification claim has gone to Trust.</summary>
    public Result SubmitForVerification(
        DateTimeOffset now, UserId requestedBy, Guid? verificationId = null)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.EmployerNotFound);
        }

        if (TrustMark.State == JobVerificationState.Pending)
        {
            return Result.Failure(JobErrors.VerificationAlreadyPending);
        }

        TrustMark = JobTrustMark.Pending(now, verificationId);
        Touch();

        RaiseDomainEvent(new EmployerVerificationRequestedDomainEvent
        {
            EmployerId = Id,
            RequestedBy = requestedBy,
            VerificationId = verificationId,
            RequestedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies an outcome Trust decided. The only path to a verified employer, reachable only
    /// from a handler for a Trust integration event, and guarded on the decision timestamp
    /// because delivery is at-least-once and unordered.
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

        if (previous == TrustMark.State)
        {
            return Result.Success();
        }

        RaiseDomainEvent(new EmployerVerificationChangedDomainEvent
        {
            EmployerId = Id,
            PreviousState = previous,
            CurrentState = TrustMark.State,
            VerificationId = verificationId,
            Badge = badge,
            Reason = reason,
            ChangedAt = decidedAt
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Responsiveness — measured, never a revision bump
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records how long this employer took to answer an application for the first time.</summary>
    public void RecordFirstResponse(TimeSpan elapsed) =>
        Responsiveness = Responsiveness.WithResponse(elapsed);

    /// <summary>Records that an application reached a decision at this employer's hand.</summary>
    public void RecordDecision() => Responsiveness = Responsiveness.WithDecision();

    /// <summary>Records an application left unanswered past the staleness threshold.</summary>
    public void RecordIgnoredApplication() => Responsiveness = Responsiveness.WithIgnored();

    public void RecordOfferExtended() => Responsiveness = Responsiveness.WithOfferExtended();

    public void RecordOfferAccepted() => Responsiveness = Responsiveness.WithOfferAccepted();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Status = EmployerStatus.Closed;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = EmployerStatus.Active;
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

    private int OwnerCount => _members.Count(member => member.Role == EmployerMemberRole.Owner);

    private void Touch() => Revision++;

    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.EmployerNotFound);
        }

        return EnsureRevision(baseRevision);
    }

    private Result MoveTo(
        EmployerStatus target, DateTimeOffset now, UserId? changedBy, string? reason)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.EmployerNotFound);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        var previous = Status;
        Status = target;
        Touch();

        RaiseDomainEvent(new EmployerStatusChangedDomainEvent
        {
            EmployerId = Id,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }
}
