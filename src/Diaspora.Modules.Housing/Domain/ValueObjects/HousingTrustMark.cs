using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What Trust has said, cached where Housing can read it without asking.
/// <para>
/// <b>Every field here is written by a Trust outcome and by nothing else.</b> There is no
/// constructor a handler can reach that produces a verified mark, and that is the point: a module
/// that could mark its own landlords verified would be the platform vouching for whoever asked.
/// The only path to <see cref="HousingVerificationState.Verified"/> is <see cref="FromOutcome"/>,
/// called from the Trust integration-event handler.
/// </para>
/// <para>
/// <b>It is a cache and it is allowed to be stale, but only in the safe direction.</b> A
/// revocation arrives as an event and clears the mark; between Trust's decision and the event's
/// delivery the platform may still show a badge for a second or two. What it will never do is show
/// one Trust never granted.
/// </para>
/// </summary>
public sealed class HousingTrustMark : ValueObject
{
    private HousingTrustMark(
        HousingVerificationState state,
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

    private HousingTrustMark() { }

    public HousingVerificationState State { get; private set; }

    /// <summary>Trust's case identifier. The correlation key an outcome arrives against.</summary>
    public Guid? VerificationId { get; private set; }

    /// <summary>The badge Trust granted, verbatim. Housing never invents one.</summary>
    public string? Badge { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Trust's own words, passed through so a claimant can fix what was wrong.</summary>
    public string? Reason { get; private set; }

    /// <summary>Whether the platform will show a badge for this.</summary>
    public bool IsVerified => State == HousingVerificationState.Verified;

    /// <summary>Whether a case is open, which is what stops a second submission.</summary>
    public bool IsPending => State == HousingVerificationState.Pending;

    /// <summary>
    /// Never verified.
    /// <para>
    /// A fresh instance per access. This is an owned type, and a shared static instance assigned
    /// to two aggregates makes EF write nulls into one of them.
    /// </para>
    /// </summary>
    public static HousingTrustMark Unverified =>
        new(HousingVerificationState.Unverified, null, null, null, null);

    /// <summary>Records that a claim has been lodged with Trust.</summary>
    public static HousingTrustMark Submitted(Guid verificationId, DateTimeOffset submittedAt) =>
        new(HousingVerificationState.Pending, verificationId, null, submittedAt, null);

    /// <summary>Applies Trust's decision. The only route to a verified mark.</summary>
    public static HousingTrustMark FromOutcome(
        HousingVerificationState state,
        Guid? verificationId,
        string? badge,
        DateTimeOffset decidedAt,
        string? reason) =>
        new(state,
            verificationId,
            Clamp(badge, HousingLimits.MaxDisplayName),
            decidedAt,
            Clamp(reason, HousingLimits.MaxNoteLength));

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
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
