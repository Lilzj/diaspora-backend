using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Policies;
using Diaspora.Domain.Identity.Trust;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Domain.Identity.Users;

/// <summary>
/// A Diaspora member. The aggregate root for identity, credentials and progressive access.
/// <para>
/// Every meaningful change goes through a method on this type; there are no public
/// setters. Expected failures come back as <see cref="Result"/> rather than exceptions,
/// matching the platform kernel's convention, so a handler can turn a refused transition
/// into a ProblemDetails response without a try/catch.
/// </para>
/// <para>
/// Sessions and devices are separate aggregates that reference this one. Loading a
/// member to authenticate a request must not drag in years of session history.
/// </para>
/// </summary>
public sealed class User : AggregateRoot<UserId>, IAuditableEntity, ISoftDeletable
{
    public const int MaxStatusReasonLength = 512;

    private readonly List<PasswordHistoryEntry> _passwordHistory = [];

    // ─── Profile ────────────────────────────────────────────────────────
    public FullName FullName { get; private set; } = default!;
    public PhoneNumber PhoneNumber { get; private set; } = default!;

    /// <summary>Optional at registration; Diaspora onboards on a phone number alone.</summary>
    public EmailAddress? Email { get; private set; }

    public CountryCode Nationality { get; private set; } = default!;
    public CountryCode CountryOfResidence { get; private set; } = default!;
    public LanguageCode PreferredLanguage { get; private set; } = default!;
    public TimeZoneIdentifier PreferredTimeZone { get; private set; } = default!;
    public AvatarReference? Avatar { get; private set; }

    // ─── Credentials & security ─────────────────────────────────────────
    public PasswordHash PasswordHash { get; private set; } = default!;
    public UserSecurityProfile Security { get; private set; } = default!;
    public IReadOnlyList<PasswordHistoryEntry> PasswordHistory => _passwordHistory.AsReadOnly();

    // ─── Authorisation & progressive access ─────────────────────────────
    public UserRole Role { get; private set; } = default!;
    public AccessState AccessState { get; private set; } = default!;

    /// <summary>
    /// The rung the member was on before a suspension or hard lock, so reinstatement
    /// puts them back where they were instead of demoting them to limited access.
    /// </summary>
    public AccessState? PreviousAccessState { get; private set; }

    public string? StatusReason { get; private set; }
    public DateTimeOffset? SuspendedUntil { get; private set; }

    /// <summary>Coarse status projected from <see cref="AccessState"/>; never stored separately.</summary>
    public AccountStatus AccountStatus => AccessState.ToAccountStatus();

    // ─── Activity ───────────────────────────────────────────────────────
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset? LastActiveAt { get; private set; }

    // ─── Auditing & soft delete ─────────────────────────────────────────
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private User() { }

    private User(
        UserId id,
        FullName fullName,
        PhoneNumber phoneNumber,
        PasswordHash passwordHash,
        CountryCode nationality,
        CountryCode countryOfResidence,
        LanguageCode preferredLanguage,
        TimeZoneIdentifier preferredTimeZone,
        DateTimeOffset now) : base(id)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        Nationality = nationality;
        CountryOfResidence = countryOfResidence;
        PreferredLanguage = preferredLanguage;
        PreferredTimeZone = preferredTimeZone;
        Role = UserRole.CommunityUser;
        AccessState = AccessState.RegistrationStarted;
        Security = UserSecurityProfile.Initial(now);
        LastActiveAt = now;
    }

    /// <summary>
    /// Creates an account. The member lands on <see cref="AccessState.RegistrationStarted"/>
    /// and holds no usable access until the phone OTP is dispatched and confirmed.
    /// </summary>
    public static User Register(
        FullName fullName,
        PhoneNumber phoneNumber,
        PasswordHash passwordHash,
        CountryCode nationality,
        CountryCode countryOfResidence,
        LanguageCode preferredLanguage,
        TimeZoneIdentifier preferredTimeZone,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentNullException.ThrowIfNull(phoneNumber);
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(nationality);
        ArgumentNullException.ThrowIfNull(countryOfResidence);
        ArgumentNullException.ThrowIfNull(preferredLanguage);
        ArgumentNullException.ThrowIfNull(preferredTimeZone);

        var user = new User(
            UserId.New(), fullName, phoneNumber, passwordHash,
            nationality, countryOfResidence, preferredLanguage, preferredTimeZone, now);

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(
            user.Id,
            phoneNumber.Value,
            fullName.Value,
            nationality.Value,
            countryOfResidence.Value,
            preferredLanguage.Value));

        return user;
    }

    // ─── Phone verification ─────────────────────────────────────────────

    /// <summary>
    /// Moves the member to <see cref="AccessState.PhoneVerificationPending"/> once the
    /// first OTP has actually been dispatched. Kept separate from registration so a
    /// failed SMS send leaves the account visibly incomplete rather than stranded in a
    /// state that claims a code is waiting.
    /// </summary>
    public Result BeginPhoneVerification(DateTimeOffset now) =>
        TransitionTo(AccessState.PhoneVerificationPending, AccessStateChangeReason.Registration, now);

    /// <summary>
    /// Confirms the phone number and promotes the member to limited access — the point
    /// at which Diaspora becomes browsable.
    /// </summary>
    public Result ConfirmPhoneVerified(DateTimeOffset now)
    {
        if (Security.PhoneVerifiedAt is not null)
        {
            return Result.Failure(UserErrors.PhoneAlreadyVerified);
        }

        var transition = TransitionTo(
            AccessState.LimitedAccess, AccessStateChangeReason.PhoneVerified, now);
        if (transition.IsFailure)
        {
            return transition;
        }

        Security = Security.WithPhoneVerified(now);
        RaiseDomainEvent(new UserPhoneVerifiedDomainEvent(Id, PhoneNumber.Value, now));

        return Result.Success();
    }

    public bool IsPhoneVerified => Security.PhoneVerifiedAt is not null;

    public bool IsEmailVerified => Email is not null && Security.EmailVerifiedAt is not null;

    /// <summary>
    /// Records that the member opened a verification link sent to <paramref name="address"/>.
    /// <para>
    /// Refused when the address on the account is no longer the one the link was sent to: a link
    /// proves ownership of one address, and the member may have changed it since. Succeeds without
    /// change when already verified, because a member who taps the same link twice has done
    /// nothing wrong.
    /// </para>
    /// </summary>
    public Result ConfirmEmailVerified(EmailAddress address, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (Email is null || Email != address)
        {
            return Result.Failure(UserErrors.EmailVerificationAddressChanged);
        }

        if (IsEmailVerified) return Result.Success();

        Security = Security.WithEmailVerified(now);
        RaiseDomainEvent(new UserProfileUpdatedDomainEvent(Id, ["EmailVerified"], now));

        return Result.Success();
    }

    // ─── Authentication ─────────────────────────────────────────────────

    /// <summary>
    /// Whether a credential exchange may even be attempted, checked before the password
    /// is verified. Returns the same shape of failure for every refusal so the endpoint
    /// cannot be used to probe account states.
    /// </summary>
    public Result EnsureCanAuthenticate(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(UserErrors.AccountDisabled);
        }

        if (AccessState == AccessState.Locked)
        {
            return Result.Failure(UserErrors.AccountLocked);
        }

        if (AccessState == AccessState.Disabled)
        {
            return Result.Failure(UserErrors.AccountDisabled);
        }

        if (!AccessState.AllowsAuthentication)
        {
            return Result.Failure(UserErrors.AuthenticationNotAllowed);
        }

        if (Security.IsLockedOut(now))
        {
            return Result.Failure(UserErrors.TemporarilyLockedOut(Security.LockoutRemaining(now)));
        }

        return Result.Success();
    }

    /// <summary>
    /// Records a failed credential exchange, tripping the temporary lockout once the
    /// policy's attempt budget is spent. The access state is untouched: a forgotten
    /// password must not push anyone off the progressive access ladder.
    /// </summary>
    public void RecordFailedLogin(DateTimeOffset now, LockoutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        Security = Security.RecordFailedLogin(now, policy.MaxFailedAttempts, policy.LockoutDuration);

        RaiseDomainEvent(new UserLoginFailedDomainEvent(
            Id, Security.FailedLoginAttempts, Security.IsLockedOut(now), now));
    }

    public void RecordSuccessfulLogin(SessionId sessionId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        Security = Security.ClearFailedLogins();
        LastLoginAt = now;
        LastActiveAt = now;

        RaiseDomainEvent(new UserLoggedInDomainEvent(Id, sessionId, AccessState.Name, now));
    }

    public void RecordActivity(DateTimeOffset now) => LastActiveAt = now;

    // ─── Password management ────────────────────────────────────────────

    /// <summary>
    /// Tests a candidate password against retired hashes. The predicate is supplied by
    /// the caller because only the infrastructure hasher knows how to compare them —
    /// the domain holds the history without being able to read it.
    /// </summary>
    public bool MatchesHistoricalPassword(Func<PasswordHash, bool> matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        return _passwordHistory.Any(entry => matcher(entry.Hash));
    }

    public bool MatchesCurrentPassword(Func<PasswordHash, bool> matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        return matcher(PasswordHash);
    }

    /// <summary>
    /// Replaces the password. Rotates the security stamp, which invalidates every access
    /// token already in the wild — callers are expected to revoke sessions too.
    /// </summary>
    public Result ChangePassword(
        PasswordHash newHash,
        DateTimeOffset now,
        PasswordHistoryPolicy historyPolicy,
        bool wasReset = false)
    {
        ArgumentNullException.ThrowIfNull(newHash);
        ArgumentNullException.ThrowIfNull(historyPolicy);

        if (PasswordHash == newHash)
        {
            return Result.Failure(UserErrors.SamePassword);
        }

        _passwordHistory.Add(PasswordHistoryEntry.Create(Id, PasswordHash, now));
        TrimPasswordHistory(historyPolicy);

        PasswordHash = newHash;
        Security = Security.WithPasswordChanged(now);

        RaiseDomainEvent(new UserPasswordChangedDomainEvent(Id, wasReset, now));

        return Result.Success();
    }

    /// <summary>
    /// Re-stores the same password under stronger hashing parameters.
    /// <para>
    /// Not a password change: no history entry, no security stamp rotation, no event. The
    /// member's credential is unchanged — only its representation improved — so nothing
    /// they hold should stop working.
    /// </para>
    /// </summary>
    public void UpgradePasswordHash(PasswordHash rehashed)
    {
        ArgumentNullException.ThrowIfNull(rehashed);
        PasswordHash = rehashed;
    }

    /// <summary>
    /// Forces a password change on next sign-in. A hook for a future expiry policy;
    /// nothing in B2 calls it automatically.
    /// </summary>
    public void RequirePasswordChange() => Security = Security.WithPasswordChangeRequired();

    public bool IsPasswordExpired(DateTimeOffset now, TimeSpan? maxAge) =>
        Security.IsPasswordExpired(now, maxAge);

    private void TrimPasswordHistory(PasswordHistoryPolicy policy)
    {
        if (policy.Depth <= 0)
        {
            _passwordHistory.Clear();
            return;
        }

        var excess = _passwordHistory.Count - policy.Depth;
        if (excess <= 0) return;

        var stale = _passwordHistory
            .OrderBy(entry => entry.RetiredAt)
            .Take(excess)
            .ToList();

        foreach (var entry in stale)
        {
            _passwordHistory.Remove(entry);
        }
    }

    // ─── Profile ────────────────────────────────────────────────────────

    public Result UpdateProfile(
        FullName fullName,
        CountryCode nationality,
        CountryCode countryOfResidence,
        LanguageCode preferredLanguage,
        TimeZoneIdentifier preferredTimeZone,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentNullException.ThrowIfNull(nationality);
        ArgumentNullException.ThrowIfNull(countryOfResidence);
        ArgumentNullException.ThrowIfNull(preferredLanguage);
        ArgumentNullException.ThrowIfNull(preferredTimeZone);

        var changed = new List<string>();

        if (FullName != fullName) { FullName = fullName; changed.Add(nameof(FullName)); }
        if (Nationality != nationality) { Nationality = nationality; changed.Add(nameof(Nationality)); }
        if (CountryOfResidence != countryOfResidence)
        {
            CountryOfResidence = countryOfResidence;
            changed.Add(nameof(CountryOfResidence));
        }
        if (PreferredLanguage != preferredLanguage)
        {
            PreferredLanguage = preferredLanguage;
            changed.Add(nameof(PreferredLanguage));
        }
        if (PreferredTimeZone != preferredTimeZone)
        {
            PreferredTimeZone = preferredTimeZone;
            changed.Add(nameof(PreferredTimeZone));
        }

        if (changed.Count > 0)
        {
            RaiseDomainEvent(new UserProfileUpdatedDomainEvent(Id, changed, now));
        }

        return Result.Success();
    }

    public Result SetEmail(EmailAddress? email, DateTimeOffset now)
    {
        if (Email == email) return Result.Success();

        Email = email;

        // A proof of ownership belongs to the address that received the link. Without this, the
        // first address's verification would carry over to whatever replaced it (found 5 Oct 2026,
        // when verification was first wired: nothing had ever set the timestamp, so nothing had
        // ever needed clearing).
        Security = Security.WithEmailUnverified();
        RaiseDomainEvent(new UserProfileUpdatedDomainEvent(Id, [nameof(Email)], now));

        return Result.Success();
    }

    public Result SetAvatar(AvatarReference? avatar, DateTimeOffset now)
    {
        if (Avatar == avatar) return Result.Success();

        Avatar = avatar;
        RaiseDomainEvent(new UserProfileUpdatedDomainEvent(Id, [nameof(Avatar)], now));

        return Result.Success();
    }

    public void AssignRole(UserRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        Role = role;
    }

    // ─── Verification (Trust Engine boundary) ───────────────────────────

    /// <summary>
    /// Records that the member has asked to be verified. This is the only verification
    /// decision Identity is entitled to make — that a request exists. The verdict is the
    /// Trust Engine's, delivered back through <see cref="ApplyTrustDecision"/>.
    /// </summary>
    public Result RequestVerification(DateTimeOffset now)
    {
        if (AccessState.IsAwaitingTrustDecision)
        {
            return Result.Failure(UserErrors.VerificationAlreadyRequested);
        }

        if (AccessState != AccessState.LimitedAccess)
        {
            return Result.Failure(UserErrors.NotEligibleForVerification);
        }

        var transition = TransitionTo(
            AccessState.VerificationPending, AccessStateChangeReason.VerificationRequested, now);
        if (transition.IsFailure)
        {
            return transition;
        }

        RaiseDomainEvent(new UserVerificationRequestedDomainEvent(
            Id, CountryOfResidence.Value, Nationality.Value, now));

        return Result.Success();
    }

    /// <summary>
    /// Applies a verdict from the Trust &amp; Verification Engine. Identity translates the
    /// outcome into a position on the access ladder and nothing more — it does not know
    /// or store why the decision went the way it did beyond the supplied reason string.
    /// </summary>
    public Result ApplyTrustDecision(TrustDecision decision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var (target, reason) = decision.Outcome switch
        {
            TrustDecisionOutcome.Submitted =>
                (AccessState.VerificationPending, AccessStateChangeReason.VerificationRequested),
            TrustDecisionOutcome.UnderReview =>
                (AccessState.VerificationUnderReview, AccessStateChangeReason.VerificationReviewStarted),
            TrustDecisionOutcome.Granted =>
                (AccessState.Verified, AccessStateChangeReason.TrustDecisionGranted),
            TrustDecisionOutcome.Rejected =>
                (AccessState.LimitedAccess, AccessStateChangeReason.TrustDecisionRejected),
            TrustDecisionOutcome.Revoked =>
                (AccessState.LimitedAccess, AccessStateChangeReason.TrustRevoked),
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision), decision.Outcome, "Unrecognised trust decision outcome.")
        };

        if (decision.Outcome == TrustDecisionOutcome.Granted)
        {
            // A repeat of a grant already applied is not a refusal: it is how a repair
            // re-sends an approval without first knowing whether it arrived.
            if (AccessState == AccessState.Verified)
            {
                return Result.Success();
            }

            // A reviewer may approve straight from the queue without assigning it first,
            // so nothing ever put the member on the review rung — and Verified is
            // reachable only from there. Every such approval was refused and logged as a
            // warning while the reviewer was told it worked (7 Oct 2026). The approval
            // is the review, so the member passes through that rung on the way.
            if (AccessState == AccessState.VerificationPending)
            {
                var review = TransitionTo(
                    AccessState.VerificationUnderReview,
                    AccessStateChangeReason.VerificationReviewStarted,
                    now);

                if (review.IsFailure)
                {
                    return review;
                }
            }
        }

        return TransitionTo(target, reason, now, decision.Reason);
    }

    // ─── Account lifecycle ──────────────────────────────────────────────

    public Result Suspend(string reason, DateTimeOffset? until, DateTimeOffset now)
    {
        var transition = TransitionTo(
            AccessState.Suspended, AccessStateChangeReason.ModerationSuspension, now, reason);
        if (transition.IsFailure)
        {
            return transition;
        }

        SuspendedUntil = until;
        RaiseDomainEvent(new UserAccountSuspendedDomainEvent(Id, StatusReason ?? string.Empty, until, now));

        return Result.Success();
    }

    /// <summary>
    /// Lifts a suspension or a hard lock, returning the member to the rung they were on
    /// when the enforcement action landed.
    /// </summary>
    public Result Reinstate(DateTimeOffset now, AccessStateChangeReason reason)
    {
        if (AccessState != AccessState.Suspended && AccessState != AccessState.Locked)
        {
            return Result.Failure(UserErrors.InvalidStateTransition(AccessState, AccessState.LimitedAccess));
        }

        var target = PreviousAccessState ?? AccessState.LimitedAccess;
        if (!AccessState.CanTransitionTo(target))
        {
            target = AccessState.LimitedAccess;
        }

        var transition = TransitionTo(target, reason, now);
        if (transition.IsFailure)
        {
            return transition;
        }

        SuspendedUntil = null;
        StatusReason = null;
        Security = Security.ClearFailedLogins();

        RaiseDomainEvent(new UserAccountReinstatedDomainEvent(Id, target.Name, now));

        return Result.Success();
    }

    /// <summary>
    /// Applies a security hard lock. Distinct from the failed-login lockout: this one
    /// does not expire on a timer and requires account recovery to clear.
    /// </summary>
    public Result Lock(string reason, DateTimeOffset now)
    {
        var transition = TransitionTo(AccessState.Locked, AccessStateChangeReason.SecurityLock, now, reason);
        if (transition.IsFailure)
        {
            return transition;
        }

        Security = Security.WithRotatedSecurityStamp();
        RaiseDomainEvent(new UserAccountLockedDomainEvent(Id, StatusReason ?? string.Empty, now));

        return Result.Success();
    }

    public Result Disable(string reason, DateTimeOffset now)
    {
        var transition = TransitionTo(
            AccessState.Disabled, AccessStateChangeReason.AccountDisabled, now, reason);
        if (transition.IsFailure)
        {
            return transition;
        }

        Security = Security.WithRotatedSecurityStamp();
        RaiseDomainEvent(new UserAccountDisabledDomainEvent(Id, StatusReason ?? string.Empty, now));

        return Result.Success();
    }

    /// <summary>
    /// Reopens a disabled account. Re-entry is always at limited access — trust has to
    /// be earned again through the Trust Engine.
    /// </summary>
    public Result Reactivate(DateTimeOffset now)
    {
        var transition = TransitionTo(
            AccessState.LimitedAccess, AccessStateChangeReason.AccountReactivated, now);
        if (transition.IsFailure)
        {
            return transition;
        }

        StatusReason = null;
        SuspendedUntil = null;

        RaiseDomainEvent(new UserAccountReinstatedDomainEvent(Id, AccessState.Name, now));

        return Result.Success();
    }

    // ─── State machine ──────────────────────────────────────────────────

    /// <summary>
    /// The single choke point for access state changes. Every transition is validated
    /// against <see cref="AccessStateTransitions"/> and announced with the same event,
    /// so no caller can move an account sideways without the platform noticing.
    /// </summary>
    private Result TransitionTo(
        AccessState target,
        AccessStateChangeReason reason,
        DateTimeOffset now,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!AccessState.CanTransitionTo(target))
        {
            return Result.Failure(UserErrors.InvalidStateTransition(AccessState, target));
        }

        var from = AccessState;

        // Only remember the rung we can meaningfully return to. Chaining Suspended into
        // Locked must not overwrite the original position with "Suspended".
        if (target.IsRestricted && !from.IsRestricted)
        {
            PreviousAccessState = from;
        }
        else if (!target.IsRestricted)
        {
            PreviousAccessState = null;
        }

        AccessState = target;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            var trimmed = notes.Trim();
            StatusReason = trimmed.Length <= MaxStatusReasonLength
                ? trimmed
                : trimmed[..MaxStatusReasonLength];
        }

        RaiseDomainEvent(new UserAccessStateChangedDomainEvent(
            Id, from.Name, target.Name, reason, StatusReason, now));

        return Result.Success();
    }

    // ─── Erasure ────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces every field that names a person, and closes the account for good.
    /// <para>
    /// <b>Anonymisation rather than deletion, and the reason is the schema.</b> Two hundred and
    /// sixteen foreign keys across this platform point at a member: a listing has a seller, a message
    /// has an author, a ledger entry has an actor. Deleting the row would either break all of them or
    /// cascade into destroying content other members are still reading. What a member is entitled to
    /// is that the platform stops holding a person; they are not entitled to delete a conversation
    /// somebody else took part in.
    /// </para>
    /// <para>
    /// So the row survives, the references stay valid, and everything that identified a human being is
    /// gone: name, phone number, email, avatar. The security stamp is rotated, which invalidates every
    /// access token already issued, and the account is closed and soft-deleted so nothing can sign in
    /// again or reactivate it.
    /// </para>
    /// <para>
    /// <paramref name="anonymizedPhoneNumber"/> is supplied rather than derived here because it has to
    /// be <em>unique</em> — <c>ix_users_phone_number</c> is unconditionally unique and does not exclude
    /// deleted rows, so two erased members cannot share a placeholder. The caller draws it from a
    /// sequence; the aggregate is not the place to reach for one.
    /// </para>
    /// <para>
    /// <b>Irreversible by construction.</b> <see cref="Restore"/> would clear the soft-delete flag and
    /// give back an account whose profile is tombstones — so erasure closes the account first, and
    /// <see cref="Reactivate"/> from <see cref="AccessState.Disabled"/> would return a member to
    /// limited access with no name and a phone number nobody can dial. Nothing calls either on an
    /// erased account, and <see cref="IsAnonymized"/> exists so a caller can refuse to.
    /// </para>
    /// </summary>
    public Result Anonymize(
        PhoneNumber anonymizedPhoneNumber, FullName tombstoneName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(anonymizedPhoneNumber);
        ArgumentNullException.ThrowIfNull(tombstoneName);

        if (IsAnonymized)
        {
            // Erasing twice is not an error. A member whose request was interrupted and retried should
            // get the same answer as one whose request completed, and a second pass must not raise a
            // second event or rotate a stamp that already invalidated everything.
            return Result.Success();
        }

        if (AccessState != AccessState.Disabled)
        {
            var closed = Disable("Erased at the member's request.", now);
            if (closed.IsFailure)
            {
                return closed;
            }
        }

        FullName = tombstoneName;
        PhoneNumber = anonymizedPhoneNumber;
        Email = null;
        Avatar = null;
        LastLoginAt = null;
        LastActiveAt = null;
        StatusReason = ErasedStatusReason;

        // The history is a list of hashes of passwords this person chose, and a password is reused
        // across services often enough that keeping the hashes of somebody who has left is a liability
        // with no purpose.
        _passwordHistory.Clear();

        Security = Security.WithRotatedSecurityStamp();

        MarkDeleted(now, Id.Value.ToString());

        RaiseDomainEvent(new UserAnonymizedDomainEvent(Id, now));

        return Result.Success();
    }

    /// <summary>The status reason an erased account carries, and the marker that it is one.</summary>
    public const string ErasedStatusReason = "Erased at the member's request.";

    /// <summary>
    /// Whether this account has been erased. Read before offering anything that assumes a person is
    /// behind it — reactivation above all.
    /// </summary>
    public bool IsAnonymized =>
        IsDeleted &&
        AccessState == AccessState.Disabled &&
        string.Equals(StatusReason, ErasedStatusReason, StringComparison.Ordinal);

    // ─── Auditing & soft delete ─────────────────────────────────────────

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

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Security = Security.WithRotatedSecurityStamp();
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
