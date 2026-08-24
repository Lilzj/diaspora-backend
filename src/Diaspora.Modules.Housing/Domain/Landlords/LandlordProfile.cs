using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Landlords;

/// <summary>
/// Who is letting the property.
/// <para>
/// <b>A profile rather than a member, because "landlord" is a role a member takes on and not a
/// kind of member.</b> One person may let a spare room; the same person may also run a letting
/// agency, and B3.0.2's subject model exists precisely so the two can be verified separately. The
/// profile is what carries the trading name, the badge, the record and the suspension — and a
/// member with no profile simply cannot list a property, which is the check
/// <c>HousingAuthorizationService.RequireLandlordAsync</c> makes once for every write handler.
/// </para>
/// <para>
/// <b>One profile per member, and that is a deliberate simplification.</b> An agency with several
/// brands is a real thing and is not modelled here; it is modelled as several B7 providers behind
/// one profile, which is where business identity already lives. Relaxing this later is additive —
/// the unique index is on (owner), not on anything a listing points at.
/// </para>
/// <para>
/// <b>The badge is Trust's and the record is Housing's.</b> <see cref="TrustMark"/> is written
/// only by an outcome from B3; <see cref="Statistics"/> is what this module watched happen. A
/// landlord cannot influence either by writing anything.
/// </para>
/// </summary>
public sealed class LandlordProfile
    : AggregateRoot<LandlordProfileId>, IAuditableEntity, ISoftDeletable
{
    private LandlordProfile(
        LandlordProfileId id,
        UserId ownerUserId,
        LandlordIdentity identity,
        HousingSlug slug,
        string primaryCountryCode,
        LandlordContact contact,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerUserId = ownerUserId;
        Identity = identity;
        Slug = slug;
        PrimaryCountryCode = primaryCountryCode;
        Contact = contact;
        TrustMark = HousingTrustMark.Unverified;
        Statistics = LandlordStatistics.Empty;
        ReviewSummary = LandlordReviewSummary.Empty;
        Status = LandlordStatus.Active;
        CreatedAt = createdAt;
    }

    private LandlordProfile() { }

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    /// <summary>The member behind the profile. Unique: one profile per member.</summary>
    public UserId OwnerUserId { get; private set; } = default!;

    public LandlordIdentity Identity { get; private set; } = default!;

    /// <summary>The profile's stable web address, and the tie-break every landlord page orders on.</summary>
    public HousingSlug Slug { get; private set; } = default!;

    /// <summary>Where they mostly let. Submitted to Trust as the jurisdiction of the claim.</summary>
    public string PrimaryCountryCode { get; private set; } = string.Empty;

    public LandlordContact Contact { get; private set; } = default!;

    /// <summary>
    /// The B7 provider behind an agency, a hostel or an employer. Null for an individual.
    /// <para>
    /// A reference, never a copy. The business itself — its services, its hours, its own
    /// verification — belongs to Providers, and duplicating any of it here would create a second
    /// truth that drifts.
    /// </para>
    /// </summary>
    public Guid? ProviderId { get; private set; }

    public string? Bio { get; private set; }

    public Guid? LogoMediaId { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public LandlordStatus Status { get; private set; }

    public HousingTrustMark TrustMark { get; private set; } = default!;

    public LandlordStatistics Statistics { get; private set; } = default!;

    /// <summary>
    /// The rating rollup, recomputed from the reviews table rather than incremented.
    /// <para>
    /// <b>Read <c>ReviewSummary.HasReviews</c> before rendering the average.</b> A landlord with
    /// no reviews has an average of zero, and zero is not a rating — the housing screen rendered
    /// <c>★ {listing.host.rating}</c> against a field that never existed, which is the defect
    /// this rollup exists to end rather than repeat.
    /// </para>
    /// </summary>
    public LandlordReviewSummary ReviewSummary { get; private set; } = default!;

    /// <summary>Why they were suspended, for the landlord to read and a moderator to review.</summary>
    public string? SuspensionReason { get; private set; }

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

    /// <summary>Whether this landlord may list, edit and take reservations.</summary>
    public bool CanLet => !IsDeleted && Status == LandlordStatus.Active;

    /// <summary>Whether the platform shows a badge. Trust's answer, cached.</summary>
    public bool IsVerified => TrustMark.IsVerified;

    /// <summary>Whether this is an agency, hostel or employer rather than a person.</summary>
    public bool IsBusiness => Identity.IsBusiness;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<LandlordProfile> Register(
        LandlordProfileId id,
        UserId ownerUserId,
        LandlordIdentity identity,
        HousingSlug slug,
        string? primaryCountryCode,
        LandlordContact contact,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ownerUserId);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(contact);

        var country = HousingLocationContext.NormalizeCountry(primaryCountryCode);

        if (country is null)
        {
            return Result.Failure<LandlordProfile>(HousingErrors.CountryInvalid);
        }

        var profile = new LandlordProfile(
            id, ownerUserId, identity, slug, country, contact, createdAt);

        profile.RaiseDomainEvent(new LandlordRegisteredDomainEvent
        {
            LandlordId = id,
            OwnerUserId = ownerUserId,
            DisplayName = identity.DisplayName,
            Kind = identity.Kind,
            ProviderId = null
        });

        return Result.Success(profile);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result<IReadOnlyList<string>> Update(
        int? baseRevision,
        LandlordIdentity? identity = null,
        LandlordContact? contact = null,
        string? bio = null,
        string? primaryCountryCode = null)
    {
        var guard = EnsureRevision(baseRevision);

        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(guard.Error);
        }

        if (IsDeleted)
        {
            return Result.Failure<IReadOnlyList<string>>(HousingErrors.LandlordNotFound);
        }

        var changed = new List<string>();

        if (identity is not null && identity != Identity)
        {
            Identity = identity;
            changed.Add(nameof(Identity));
        }

        if (contact is not null && contact != Contact)
        {
            Contact = contact;
            changed.Add(nameof(Contact));
        }

        if (bio is not null)
        {
            var trimmed = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();

            if (trimmed != Bio)
            {
                Bio = trimmed;
                changed.Add(nameof(Bio));
            }
        }

        if (primaryCountryCode is not null
            && HousingLocationContext.NormalizeCountry(primaryCountryCode) is { } country
            && !string.Equals(country, PrimaryCountryCode, StringComparison.Ordinal))
        {
            PrimaryCountryCode = country;
            changed.Add(nameof(PrimaryCountryCode));
        }

        if (changed.Count == 0)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        Revision++;

        RaiseDomainEvent(new LandlordUpdatedDomainEvent
        {
            LandlordId = Id,
            OwnerUserId = OwnerUserId,
            ChangedFields = changed
        });

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    /// <summary>Sets the profile's logo. A Media reference, never bytes.</summary>
    public void SetLogo(Guid? logoMediaId)
    {
        LogoMediaId = logoMediaId is { } id && id != Guid.Empty ? id : null;
        Revision++;
    }

    /// <summary>
    /// Links the profile to a B7 provider.
    /// <para>
    /// The caller has already confirmed the provider exists and belongs to this member — Housing
    /// cannot check ownership of another module's aggregate, and asking Providers is the resolver's
    /// job rather than the aggregate's.
    /// </para>
    /// </summary>
    public Result LinkProvider(Guid? providerId, int? baseRevision)
    {
        var guard = EnsureRevision(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        ProviderId = providerId is { } id && id != Guid.Empty ? id : null;
        Revision++;

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Status
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bars the landlord from letting.
    /// <para>
    /// Their existing listings are not archived here, deliberately: a suspension is a decision
    /// about a person, and archiving twenty properties as a side effect of it would take down
    /// tenancies that are already agreed. The moderation handler decides what happens to the
    /// listings, separately and visibly.
    /// </para>
    /// </summary>
    public void Suspend(string? reason, UserId? moderatorId, DateTimeOffset now) =>
        ChangeStatus(LandlordStatus.Suspended, reason, moderatorId, now);

    public void Reinstate(UserId? moderatorId, DateTimeOffset now) =>
        ChangeStatus(LandlordStatus.Active, null, moderatorId, now);

    public void Close(DateTimeOffset now) =>
        ChangeStatus(LandlordStatus.Closed, null, null, now);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records that a verification claim has been lodged for this landlord.</summary>
    public Result SubmitForVerification(
        Guid verificationId, UserId requestedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (TrustMark.IsPending)
        {
            return Result.Failure(HousingErrors.VerificationAlreadyPending);
        }

        TrustMark = HousingTrustMark.Submitted(verificationId, now);
        ModifiedAt = now;

        RaiseDomainEvent(new HousingVerificationRequestedDomainEvent
        {
            LandlordId = Id,
            ListingId = null,
            RequestedBy = requestedBy,
            VerificationId = verificationId
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies Trust's decision. The only route to a verified landlord, and called only from the
    /// Trust outcome handler.
    /// </summary>
    public void ApplyVerificationOutcome(
        HousingVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset now)
    {
        var previous = TrustMark.State;

        if (previous == state)
        {
            return;
        }

        TrustMark = HousingTrustMark.FromOutcome(state, verificationId, badge, now, reason);
        ModifiedAt = now;

        RaiseDomainEvent(new HousingVerificationChangedDomainEvent
        {
            LandlordId = Id,
            ListingId = null,
            PreviousState = previous,
            CurrentState = state,
            VerificationId = verificationId,
            Badge = badge,
            Reason = reason,
            ChangedAt = now
        });
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Record
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void RecordListingCounts(int activeListings, int totalListings) =>
        Statistics = Statistics.WithListingCounts(activeListings, totalListings);

    public void RecordLet() => Statistics = Statistics.RecordLet();

    /// <summary>
    /// Replaces the rating rollup wholesale.
    /// <para>
    /// Takes a whole summary rather than a delta on purpose: the only supported way to change
    /// this field is to recompute it from the reviews, so there is no method here that could
    /// drift it.
    /// </para>
    /// </summary>
    public void ApplyReviewSummary(LandlordReviewSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ReviewSummary = summary;
    }

    public void RecordViewingRequest() => Statistics = Statistics.RecordViewingRequest();

    public void RecordViewingAnswered(DateTimeOffset answeredAt) =>
        Statistics = Statistics.RecordViewingAnswered(answeredAt);

    /// <summary>
    /// Records that the landlord did not attend a viewing they had agreed to. Fed to the fraud
    /// seam and shown to moderators, never rendered as a public number.
    /// </summary>
    public void RecordNoShow() => Statistics = Statistics.RecordNoShow();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

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

    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(HousingErrors.RevisionConflict)
            : Result.Success();

    private void ChangeStatus(
        LandlordStatus status, string? reason, UserId? changedBy, DateTimeOffset now)
    {
        if (Status == status)
        {
            return;
        }

        var previous = Status;

        Status = status;
        SuspensionReason = status == LandlordStatus.Suspended ? reason : null;
        ModifiedAt = now;

        RaiseDomainEvent(new LandlordStatusChangedDomainEvent
        {
            LandlordId = Id,
            OwnerUserId = OwnerUserId,
            PreviousStatus = previous,
            CurrentStatus = status,
            ChangedBy = changedBy,
            Reason = reason
        });
    }
}
