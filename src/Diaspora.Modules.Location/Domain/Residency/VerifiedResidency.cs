using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Residency;

/// <summary>
/// What the Trust Engine has decided about a member's residency in one country, as this
/// module last heard it.
/// <para>
/// <b>A projection, not a decision.</b> Every transition here is caused by an integration
/// event Trust published — <c>VerificationApproved</c>, <c>VerificationRejected</c>,
/// <c>VerificationExpired</c>, <c>VerificationRevoked</c>. Nothing in this module ever
/// computes a verification outcome, and the type has no method that would let it: the only
/// mutators take an event's payload and record it.
/// </para>
/// <para>
/// <b>Why hold a copy at all.</b> The alternative is calling Trust on the read path of every
/// experience timeline, contributor profile and country page — a synchronous cross-module
/// dependency on a screen that renders a dozen members at once. Event-carried state transfer
/// is the standard answer, and it is not duplication of trust <em>logic</em>: this row cannot
/// decide anything, it can only remember what was decided. If it disagrees with Trust, Trust
/// is right and the next event corrects it.
/// </para>
/// <para>
/// <b>At-least-once, out-of-order.</b> Delivery guarantees nothing about ordering, so
/// <see cref="DecidedAt"/> guards every write: an event older than what is already recorded
/// is ignored rather than applied. Without that, a retried approval can resurrect a
/// revocation.
/// </para>
/// </summary>
public sealed class VerifiedResidency : Entity<VerifiedResidencyId>
{
    public UserId UserId { get; private set; } = default!;

    public CountryCode CountryCode { get; private set; } = default!;

    public ResidencyVerificationState State { get; private set; } =
        ResidencyVerificationState.Unverified;

    /// <summary>Trust's identifier for the request behind the current state.</summary>
    public Guid? VerificationId { get; private set; }

    /// <summary>When Trust made the decision this row reflects. The ordering guard.</summary>
    public DateTimeOffset DecidedAt { get; private set; }

    /// <summary>When a grant lapses. Null when it does not.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Trust's reason code for a rejection or revocation, verbatim.</summary>
    public string? Reason { get; private set; }

    /// <summary>When this module recorded the change. Diagnostic; never a business rule.</summary>
    public DateTimeOffset ObservedAt { get; private set; }

    private VerifiedResidency() { }

    private VerifiedResidency(
        VerifiedResidencyId id, UserId userId, CountryCode countryCode) : base(id)
    {
        UserId = userId;
        CountryCode = countryCode;
    }

    public static VerifiedResidency For(UserId userId, CountryCode countryCode)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(countryCode);

        return new VerifiedResidency(VerifiedResidencyId.New(), userId, countryCode);
    }

    /// <summary>
    /// Whether the residency counts as proven right now.
    /// <para>
    /// Expiry is evaluated against the caller's clock rather than trusted to a background
    /// job, so a lapsed grant stops counting the moment it lapses rather than the next time
    /// a sweep runs.
    /// </para>
    /// </summary>
    public bool IsVerifiedAt(DateTimeOffset now) =>
        State == ResidencyVerificationState.Verified &&
        (ExpiresAt is null || ExpiresAt > now);

    /// <summary>
    /// Applies a decision Trust published. Ignores anything older than what is recorded.
    /// </summary>
    /// <returns>True when the projection changed.</returns>
    public bool Apply(
        ResidencyVerificationState state,
        Guid? verificationId,
        DateTimeOffset decidedAt,
        DateTimeOffset observedAt,
        DateTimeOffset? expiresAt = null,
        string? reason = null)
    {
        // Strictly older, not older-or-equal: a redelivery of the same event is harmless to
        // reapply and skipping it would drop a genuine same-instant correction.
        if (decidedAt < DecidedAt)
        {
            return false;
        }

        var unchanged = State == state &&
                        VerificationId == verificationId &&
                        ExpiresAt == expiresAt;

        State = state;
        VerificationId = verificationId;
        DecidedAt = decidedAt;
        ExpiresAt = expiresAt;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ObservedAt = observedAt;

        return !unchanged;
    }
}
