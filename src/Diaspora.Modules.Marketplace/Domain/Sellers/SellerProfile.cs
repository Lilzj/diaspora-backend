using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Sellers;

/// <summary>
/// Who is selling. The brief's "seller profile projection", made an aggregate.
/// <para>
/// <b>It exists so a listing belongs to a trading entity rather than to a login.</b> A member with
/// one profile and a shop with a storefront are the same shape here, which is what lets the module
/// treat individuals and businesses identically everywhere except in what a badge says about
/// them. It is also what makes seller suspension possible without suspending the person's account,
/// which is Identity's to do and not this module's.
/// </para>
/// <para>
/// <b><see cref="SellerType"/> is computed, never stored.</b> The brief's five seller types are a
/// projection of three facts this module does not all own: the declared kind, the verification
/// Trust decided, and whether a Providers storefront stands behind it. A stored type would keep
/// saying "Verified Business" after Trust revoked the verification, which is the platform
/// vouching for exactly the wrong party.
/// </para>
/// <para>
/// <b>One profile per member, and the uniqueness is a database constraint rather than a check.</b>
/// Two profiles for one member would split a seller's history in half — the reputation a buyer is
/// relying on would be the half the seller chose to show them.
/// </para>
/// </summary>
public sealed class SellerProfile
    : AggregateRoot<SellerProfileId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private SellerProfile(
        SellerProfileId id,
        UserId ownerId,
        SellerIdentity identity,
        ListingSlug slug,
        string countryCode,
        DateTimeOffset createdAt)
        : base(id)
    {
        OwnerUserId = ownerId;
        Identity = identity;
        Slug = slug;
        PrimaryCountryCode = countryCode;
        Contact = SellerContact.InAppOnly;
        TrustMark = ListingTrustMark.None;
        Statistics = SellerStatistics.None;
        Status = SellerStatus.Active;
        CreatedAt = createdAt;
    }

    private SellerProfile() { }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    /// <summary>The member behind the profile. One profile per member.</summary>
    public UserId OwnerUserId { get; private set; } = default!;

    /// <summary>The platform's ownership seam.</summary>
    public Guid OwnerId => OwnerUserId.Value;

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    public SellerIdentity Identity { get; private set; } = default!;

    /// <summary>The storefront handle. Stable across renames, for the same reason a listing's is.</summary>
    public ListingSlug Slug { get; private set; } = default!;

    public SellerContact Contact { get; private set; } = default!;

    /// <summary>Where the seller mostly trades. Not a restriction — listings carry their own.</summary>
    public string PrimaryCountryCode { get; private set; } = string.Empty;

    /// <summary>
    /// The Providers shop this storefront is, when there is one.
    /// <para>
    /// <b>An identifier and nothing else.</b> The shop's name, rating, opening hours and badge are
    /// B7's answers at read time; copying any of them here would be this module holding another's
    /// data, and would misrepresent a shop that has since changed.
    /// </para>
    /// </summary>
    public Guid? ProviderId { get; private set; }

    /// <summary>The logo, by reference. B5 owns the bytes.</summary>
    public Guid? LogoMediaId { get; private set; }

    /// <summary>The banner, by reference.</summary>
    public Guid? BannerMediaId { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public SellerStatus Status { get; private set; }

    public ListingTrustMark TrustMark { get; private set; } = default!;

    public SellerStatistics Statistics { get; private set; } = default!;

    /// <summary>Why the seller was suspended. Shown to them, never to buyers.</summary>
    public string? SuspensionReason { get; private set; }

    public DateTimeOffset? SuspendedAt { get; private set; }

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

    /// <summary>Whether the seller may list, sell and answer offers.</summary>
    public bool CanTrade => !IsDeleted && Status == SellerStatus.Active;

    public bool IsVerified => TrustMark.IsVerified;

    /// <summary>
    /// The badge a buyer sees. The brief's five seller types, computed from what this module and
    /// its neighbours each actually know.
    /// </summary>
    public SellerType SellerType => (Identity.Kind, IsVerified, ProviderId.HasValue) switch
    {
        // A shop in the Providers directory is the strongest signal the platform has, and it
        // outranks the others because B7 has already checked things Trust has not.
        (_, _, true) => SellerType.ProviderStore,
        (SellerKind.Business, true, _) => SellerType.VerifiedBusiness,
        (SellerKind.Business, false, _) => SellerType.Business,
        (SellerKind.Individual, true, _) => SellerType.VerifiedSeller,
        _ => SellerType.Individual
    };

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a seller profile.
    /// <para>
    /// Active from the first moment, unlike a listing: there is nothing half-written about a
    /// profile, and making somebody publish their own identity before they may use it would add a
    /// step that protects nobody.
    /// </para>
    /// </summary>
    public static Result<SellerProfile> Create(
        SellerProfileId id,
        UserId ownerId,
        SellerIdentity identity,
        ListingSlug slug,
        string? countryCode,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(slug);

        var country = ListingLocationContext.NormalizeCountry(countryCode);

        if (country is null)
        {
            return Result.Failure<SellerProfile>(MarketplaceErrors.CountryInvalid);
        }

        var profile = new SellerProfile(id, ownerId, identity, slug, country, now);

        profile.RaiseDomainEvent(new SellerRegisteredDomainEvent
        {
            SellerId = id,
            OwnerId = ownerId,
            DisplayName = identity.DisplayName,
            Slug = slug.Value,
            Kind = identity.Kind,
            CountryCode = country,
            ProviderId = null
        });

        return Result.Success(profile);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Profile
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save based on a revision the editor no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(MarketplaceErrors.RevisionConflict)
            : Result.Success();

    /// <summary>
    /// Updates what buyers see about the seller.
    /// <para>
    /// Takes the clock because one branch needs it: changing the declared kind clears an existing
    /// verification, and the cleared mark has to carry the instant it was cleared or the next Trust
    /// outcome would be compared against nothing.
    /// </para>
    /// </summary>
    public Result UpdateProfile(
        SellerIdentity identity,
        SellerContact contact,
        string? countryCode,
        DateTimeOffset now,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(contact);

        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.SellerNotFound);
        }

        var guard = EnsureRevision(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var country = ListingLocationContext.NormalizeCountry(countryCode);

        if (country is null)
        {
            return Result.Failure(MarketplaceErrors.CountryInvalid);
        }

        var changed = new List<string>();

        if (!Identity.Equals(identity))
        {
            // Changing kind changes what Trust would be verifying, so an existing verification
            // stops applying. Keeping it would let an individual verify as a person and then
            // trade as a "Verified Business".
            if (Identity.Kind != identity.Kind && TrustMark.State != ListingVerificationState.None)
            {
                TrustMark = ListingTrustMark.Cleared(
                    "The seller type changed, so the verification no longer applies.", now);
            }

            Identity = identity;
            changed.Add("identity");
        }

        if (!Contact.Equals(contact))
        {
            Contact = contact;
            changed.Add("contact");
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

        Revision++;

        RaiseDomainEvent(new SellerUpdatedDomainEvent
        {
            SellerId = Id,
            ChangedBy = changedBy,
            ChangedFields = changed
        });

        return Result.Success();
    }

    /// <summary>Sets the storefront's logo and banner, by reference.</summary>
    public Result SetBranding(Guid? logoMediaId, Guid? bannerMediaId, int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.SellerNotFound);
        }

        var guard = EnsureRevision(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        LogoMediaId = logoMediaId == Guid.Empty ? null : logoMediaId;
        BannerMediaId = bannerMediaId == Guid.Empty ? null : bannerMediaId;
        Revision++;

        RaiseDomainEvent(new SellerUpdatedDomainEvent
        {
            SellerId = Id,
            ChangedBy = null,
            ChangedFields = ["branding"]
        });

        return Result.Success();
    }

    /// <summary>
    /// Links the profile to a shop in the Providers directory.
    /// <para>
    /// <b>The handler validates that the shop exists and that the caller owns it before calling
    /// this.</b> The aggregate cannot: it would have to reach into B7, and a module that reaches
    /// into another module's aggregates from its domain layer is the coupling B2.6 exists to
    /// prevent. What the aggregate can do is refuse an empty identifier and record the link.
    /// </para>
    /// </summary>
    public Result LinkStorefront(Guid providerId, int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.SellerNotFound);
        }

        var guard = EnsureRevision(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (providerId == Guid.Empty)
        {
            return Result.Failure(MarketplaceErrors.StorefrontProviderUnknown);
        }

        if (ProviderId == providerId)
        {
            return Result.Success();
        }

        ProviderId = providerId;
        Revision++;

        RaiseDomainEvent(new SellerUpdatedDomainEvent
        {
            SellerId = Id,
            ChangedBy = null,
            ChangedFields = ["storefront"]
        });

        return Result.Success();
    }

    /// <summary>Unlinks the storefront. The profile carries on as an ordinary seller.</summary>
    public Result UnlinkStorefront(int? baseRevision)
    {
        var guard = EnsureRevision(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (ProviderId is null)
        {
            return Result.Success();
        }

        ProviderId = null;
        Revision++;

        RaiseDomainEvent(new SellerUpdatedDomainEvent
        {
            SellerId = Id,
            ChangedBy = null,
            ChangedFields = ["storefront"]
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Status
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bars the seller from trading, reversibly. The brief's seller-suspension hook.
    /// <para>
    /// <b>The listings are not touched here.</b> A seller with four hundred listings would mean
    /// loading four hundred aggregates inside one command; the status event is what a handler
    /// takes them dark from, and restoring the seller brings them back because their own state
    /// never changed.
    /// </para>
    /// </summary>
    public Result Suspend(string? reason, UserId moderatorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MarketplaceLimits.MaxModerationNote)
        {
            return Result.Failure(MarketplaceErrors.ModerationNoteInvalid);
        }

        if (Status == SellerStatus.Suspended)
        {
            return Result.Success();
        }

        var previous = Status;

        Status = SellerStatus.Suspended;
        SuspensionReason = trimmed;
        SuspendedAt = now;

        RaiseStatusChanged(previous, SellerStatus.Suspended, moderatorId, trimmed, now);

        return Result.Success();
    }

    /// <summary>Lifts a suspension. Idempotent.</summary>
    public Result Reinstate(UserId? moderatorId, DateTimeOffset now)
    {
        if (Status != SellerStatus.Suspended)
        {
            return Result.Success();
        }

        Status = SellerStatus.Active;
        SuspensionReason = null;
        SuspendedAt = null;

        RaiseStatusChanged(SellerStatus.Suspended, SellerStatus.Active, moderatorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// Closes the profile at the member's own request.
    /// <para>
    /// The profile survives as a record, because completed sales point at it and a buyer looking
    /// back at what they bought should not find a gap. Listings are archived by the handler.
    /// </para>
    /// </summary>
    public Result Close(DateTimeOffset now, UserId? closedBy = null)
    {
        if (Status == SellerStatus.Closed)
        {
            return Result.Success();
        }

        var previous = Status;
        Status = SellerStatus.Closed;

        RaiseStatusChanged(previous, SellerStatus.Closed, closedBy, null, now);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification — projection of Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records that a verification claim has gone to Trust.</summary>
    public Result SubmitForVerification(
        DateTimeOffset now, UserId requestedBy, Guid? verificationId = null)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (IsDeleted || Status == SellerStatus.Closed)
        {
            return Result.Failure(MarketplaceErrors.SellerNotFound);
        }

        if (TrustMark.State == ListingVerificationState.Pending)
        {
            return Result.Failure(MarketplaceErrors.VerificationAlreadyPending);
        }

        TrustMark = ListingTrustMark.Pending(now, verificationId);

        RaiseDomainEvent(new SellerVerificationRequestedDomainEvent
        {
            SellerId = Id,
            RequestedBy = requestedBy,
            VerificationId = verificationId,
            RequestedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies an outcome Trust decided. Reachable only from a handler for a Trust integration
    /// event, and guarded on the decision timestamp because delivery is at-least-once and
    /// unordered.
    /// </summary>
    public Result ApplyVerificationDecision(
        ListingVerificationState state,
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
            ListingVerificationState.Verified =>
                ListingTrustMark.Verified(verificationId, badge, decidedAt),
            ListingVerificationState.Revoked =>
                ListingTrustMark.Revoked(verificationId, reason, decidedAt),
            ListingVerificationState.Rejected =>
                ListingTrustMark.Rejected(verificationId, reason, decidedAt),
            ListingVerificationState.Pending =>
                ListingTrustMark.Pending(decidedAt, verificationId),
            _ => ListingTrustMark.Cleared(reason, decidedAt)
        };

        if (previous != TrustMark.State)
        {
            RaiseDomainEvent(new SellerVerificationChangedDomainEvent
            {
                SellerId = Id,
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
    // Statistics — applied by handlers, never a revision bump
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void ApplyPublishedDelta(int delta) =>
        Statistics = Statistics.WithPublishedDelta(delta);

    public void RecordSale() => Statistics = Statistics.WithSale();

    public void RecordCompletedReservation() =>
        Statistics = Statistics.WithCompletedReservation();

    public void RecordCancelledReservation() =>
        Statistics = Statistics.WithCancelledReservation();

    public void RecordAcceptedOffer() => Statistics = Statistics.WithAcceptedOffer();

    public void RecordResponse(TimeSpan elapsed) =>
        Statistics = Statistics.WithResponse(elapsed);

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

    private void RaiseStatusChanged(
        SellerStatus previous,
        SellerStatus current,
        UserId? changedBy,
        string? reason,
        DateTimeOffset now) =>
        RaiseDomainEvent(new SellerStatusChangedDomainEvent
        {
            SellerId = Id,
            PreviousStatus = previous,
            CurrentStatus = current,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });
}
