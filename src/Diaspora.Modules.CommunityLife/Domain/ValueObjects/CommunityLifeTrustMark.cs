using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// What Trust has said about an organization, as this module holds it.
/// <para>
/// <b>A projection, and only ever a projection.</b> Nothing in Community Life constructs a verified
/// mark except <see cref="FromOutcome"/>, which is called from exactly one place: the handler that
/// receives Trust's published outcome. A module that could verify itself would make every badge on
/// the platform worthless, which is the line ADR-002 draws.
/// </para>
/// <para>
/// <b>The badge string is Trust's, stored verbatim and never parsed.</b> The moment this module
/// interpreted a badge value it would have taken a share of the decision, and Trust could no longer
/// change its own vocabulary without breaking a vertical.
/// </para>
/// </summary>
public sealed class CommunityLifeTrustMark : ValueObject
{
    private CommunityLifeTrustMark(
        OrganizationVerificationState state,
        Guid? verificationId,
        string? badge,
        DateTimeOffset? decidedAt,
        string? reason)
    {
        State = state;
        VerificationId = verificationId;
        Badge = badge;
        DecidedAt = decidedAt;
        Reason = reason;
    }

    private CommunityLifeTrustMark() { }

    /// <summary>Nobody has asked. Where every organization starts.</summary>
    public static CommunityLifeTrustMark Unverified =>
        new(OrganizationVerificationState.Unverified, null, null, null, null);

    public OrganizationVerificationState State { get; private set; }

    /// <summary>Trust's case identifier. The correlation key an outcome arrives against.</summary>
    public Guid? VerificationId { get; private set; }

    /// <summary>Trust's own badge name, stored verbatim.</summary>
    public string? Badge { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? Reason { get; private set; }

    public bool IsVerified => State == OrganizationVerificationState.Verified;

    public bool IsPending => State == OrganizationVerificationState.Pending;

    /// <summary>Records that a case has been opened. The only state this module may set itself.</summary>
    public static CommunityLifeTrustMark Submitted(Guid verificationId, DateTimeOffset at) =>
        new(OrganizationVerificationState.Pending, verificationId, null, at, null);

    /// <summary>Applies Trust's decision. Called from the outcome handler and nowhere else.</summary>
    public static CommunityLifeTrustMark FromOutcome(
        OrganizationVerificationState state,
        Guid? verificationId,
        string? badge,
        DateTimeOffset at,
        string? reason) =>
        new(
            state,
            verificationId,
            Clip(badge, CommunityLifeLimits.MaxDisplayName),
            at,
            Clip(reason, CommunityLifeLimits.MaxNoteLength));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return State;
        yield return VerificationId;
        yield return Badge;
        yield return DecidedAt;
        yield return Reason;
    }
}
