using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Verification;

/// <summary>
/// One verification claim a provider holds, as B7 last heard it from Trust.
/// <para>
/// <b>A projection, and every mutation on it is <c>internal</c> for that reason.</b> Trust owns
/// the decision; this records it. The only callers of <see cref="Grant"/>, <see cref="Reject"/>,
/// <see cref="Expire"/> and <see cref="Revoke"/> are the aggregate methods driven by Trust's
/// published integration events. Nothing an owner, a manager or a staff member can do from B7's
/// API reaches them — the most they can do is <see cref="Submit"/>, which says "we have asked",
/// not "we have been granted".
/// </para>
/// <para>
/// This is the same line ADR-002 draws between Identity and Trust, and the same one B4's
/// residency projection sits on. The reason it matters here specifically: a verified badge on a
/// money-transfer business is the single most consequential piece of UI this platform renders,
/// and a module that could award itself one would make the badge worthless.
/// </para>
/// <para>
/// <see cref="ExpiresAt"/> is honoured at read time by
/// <c>ProviderVerificationSummary.From</c> rather than only by a sweeper, so a lapsed grant
/// stops showing a badge the moment it lapses.
/// </para>
/// </summary>
public sealed class ProviderVerification : Entity<ProviderVerificationId>
{
    public const int MaxReasonLength = 500;

    private ProviderVerification(ProviderVerificationId id, ProviderVerificationKind kind) : base(id) =>
        Kind = kind;

    private ProviderVerification() { }

    public ProviderVerificationKind Kind { get; private set; }

    public ProviderVerificationState State { get; private set; } = ProviderVerificationState.Unverified;

    /// <summary>Trust's own request identifier, so the two systems can be reconciled.</summary>
    public Guid? TrustVerificationId { get; private set; }

    /// <summary>Which member submitted the claim. Trust reviews people, not rows.</summary>
    public UserId? SubmittedBy { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Null when the grant does not lapse.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Trust's SCREAMING_SNAKE reason code, on a refusal.</summary>
    public string? ReasonCode { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>
    /// The certification this claim is about, for
    /// <see cref="ProviderVerificationKind.Certification"/>. Null for every other kind.
    /// </summary>
    public ProviderCertificationId? CertificationId { get; private set; }

    /// <summary>Whether this claim currently counts towards the provider's verified badge.</summary>
    public bool IsCurrent(DateTimeOffset asAt) =>
        State == ProviderVerificationState.Verified && (ExpiresAt is null || ExpiresAt > asAt);

    /// <summary>Whether a fresh submission would be accepted, or is already in flight.</summary>
    public bool IsAwaitingDecision => State == ProviderVerificationState.Pending;

    internal static ProviderVerification Create(
        ProviderVerificationId id,
        ProviderVerificationKind kind,
        ProviderCertificationId? certificationId = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new ProviderVerification(id, kind) { CertificationId = certificationId };
    }

    /// <summary>
    /// Records that the claim has gone to Trust. The only transition B7's own API can cause,
    /// and it deliberately says nothing about the outcome.
    /// </summary>
    internal void Submit(Guid trustVerificationId, UserId submittedBy, DateTimeOffset submittedAt)
    {
        TrustVerificationId = trustVerificationId;
        SubmittedBy = submittedBy;
        SubmittedAt = submittedAt;
        State = ProviderVerificationState.Pending;
        DecidedAt = null;
        ReasonCode = null;
        Reason = null;
    }

    internal void Grant(DateTimeOffset decidedAt, DateTimeOffset? expiresAt)
    {
        State = ProviderVerificationState.Verified;
        DecidedAt = decidedAt;
        ExpiresAt = expiresAt;
        ReasonCode = null;
        Reason = null;
    }

    internal void Reject(DateTimeOffset decidedAt, string? reasonCode, string? reason)
    {
        State = ProviderVerificationState.Rejected;
        DecidedAt = decidedAt;
        ExpiresAt = null;
        ReasonCode = Trim(reasonCode, 64);
        Reason = Trim(reason, MaxReasonLength);
    }

    internal void Expire(DateTimeOffset decidedAt)
    {
        State = ProviderVerificationState.Expired;
        DecidedAt = decidedAt;
    }

    internal void Revoke(DateTimeOffset decidedAt, string? reason)
    {
        State = ProviderVerificationState.Revoked;
        DecidedAt = decidedAt;
        ExpiresAt = null;
        Reason = Trim(reason, MaxReasonLength);
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
