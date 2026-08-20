using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.AccessStates;

/// <summary>
/// The member's position on Diaspora's progressive access ladder.
/// <para>
/// This is the single authoritative account state. <see cref="AccountStatus"/> is a
/// coarse projection of it, never an independent field, so the two can never disagree.
/// </para>
/// <para>
/// Note the deliberate split between <see cref="Locked"/> and a failed-login lockout.
/// A failed-login lockout is a short, self-healing timer held in the member's security
/// profile and does <em>not</em> move them off their access state. <see cref="Locked"/>
/// is an administrative or security hard lock that only account recovery clears.
/// </para>
/// </summary>
public sealed class AccessState : Enumeration
{
    /// <summary>Account row exists but the registration transaction has not completed.</summary>
    public static readonly AccessState RegistrationStarted = new(1, nameof(RegistrationStarted));

    /// <summary>Registered; awaiting the phone OTP that unlocks limited access.</summary>
    public static readonly AccessState PhoneVerificationPending = new(2, nameof(PhoneVerificationPending));

    /// <summary>Phone confirmed. Can explore Diaspora and use the Verification Center; cannot contribute.</summary>
    public static readonly AccessState LimitedAccess = new(3, nameof(LimitedAccess));

    /// <summary>Member has submitted a verification request; the Trust Engine has not picked it up yet.</summary>
    public static readonly AccessState VerificationPending = new(4, nameof(VerificationPending));

    /// <summary>The Trust Engine (B3) is actively reviewing the request.</summary>
    public static readonly AccessState VerificationUnderReview = new(5, nameof(VerificationUnderReview));

    /// <summary>Trust Engine granted verification. Full contributor access.</summary>
    public static readonly AccessState Verified = new(6, nameof(Verified));

    /// <summary>Moderation suspension. The member may sign in but only sees the suspension screen.</summary>
    public static readonly AccessState Suspended = new(7, nameof(Suspended));

    /// <summary>Security hard lock. Sign-in is refused until account recovery completes.</summary>
    public static readonly AccessState Locked = new(8, nameof(Locked));

    /// <summary>Account closed by the member or by an administrator.</summary>
    public static readonly AccessState Disabled = new(9, nameof(Disabled));

    private AccessState(int id, string name) : base(id, name) { }

    /// <summary>
    /// Whether a member in this state may exchange credentials for tokens.
    /// Suspended members can: they need to reach the appeal screen.
    /// </summary>
    public bool AllowsAuthentication =>
        this == PhoneVerificationPending ||
        this == LimitedAccess ||
        this == VerificationPending ||
        this == VerificationUnderReview ||
        this == Verified ||
        this == Suspended;

    /// <summary>
    /// Whether the member has cleared onboarding and holds a usable account.
    /// </summary>
    public bool IsOnboarded =>
        this == LimitedAccess ||
        this == VerificationPending ||
        this == VerificationUnderReview ||
        this == Verified;

    /// <summary>
    /// Whether a verification request is in flight with the Trust Engine.
    /// </summary>
    public bool IsAwaitingTrustDecision =>
        this == VerificationPending || this == VerificationUnderReview;

    /// <summary>
    /// Whether the state was reached by an enforcement action rather than by member progress.
    /// </summary>
    public bool IsRestricted =>
        this == Suspended || this == Locked || this == Disabled;

    /// <summary>
    /// The coarse account status derived from this state. Presentation and
    /// cross-module consumers use this; the state machine uses the state itself.
    /// </summary>
    public AccountStatus ToAccountStatus()
    {
        if (this == RegistrationStarted || this == PhoneVerificationPending) return AccountStatus.Provisioning;
        if (this == Suspended) return AccountStatus.Suspended;
        if (this == Locked) return AccountStatus.Locked;
        if (this == Disabled) return AccountStatus.Disabled;
        return AccountStatus.Active;
    }

    /// <summary>
    /// Whether <paramref name="target"/> is a legal next state. Transitions are
    /// declared once, in <see cref="AccessStateTransitions"/>, and nowhere else.
    /// </summary>
    public bool CanTransitionTo(AccessState target) =>
        AccessStateTransitions.IsAllowed(this, target);

    public IReadOnlyCollection<AccessState> AllowedTransitions =>
        AccessStateTransitions.AllowedFrom(this);

    public static IReadOnlyCollection<AccessState> All => Enumeration.GetAll<AccessState>().ToArray();
}
