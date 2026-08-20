using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// A projection of what Trust decided, held so a card can render a badge without a second read.
/// <para>
/// <b>Nothing in this module may construct a <see cref="ListingVerificationState.Verified"/> mark
/// except from a Trust outcome.</b> The factories below are the only way to make one, and the only
/// caller of <c>Verified</c> is the aggregate method reachable from a Trust integration event
/// handler. That is the shape of "Marketplace never owns Trust data": the state is here for
/// rendering and filtering, and the decision is somewhere else entirely.
/// </para>
/// <para>
/// <b><see cref="ChangedAt"/> is persisted because the staleness guard is only as durable as the
/// timestamp it compares against.</b> Trust delivery is at-least-once and unordered, so a delayed
/// redelivery of an old approval must not re-verify a seller Trust has since revoked.
/// </para>
/// <para>
/// The factories are properties or methods returning fresh instances rather than cached statics:
/// an owned instance shared between two aggregates in one <c>DbContext</c> is tracked against
/// whichever owner EF saw first, and the second silently writes nulls into NOT NULL columns.
/// </para>
/// </summary>
public sealed class ListingTrustMark : ValueObject
{
    private ListingTrustMark(
        ListingVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset? verifiedAt,
        DateTimeOffset? changedAt)
    {
        State = state;
        VerificationId = verificationId;
        Badge = badge;
        Reason = reason;
        VerifiedAt = verifiedAt;
        ChangedAt = changedAt;
    }

    private ListingTrustMark() { }

    public ListingVerificationState State { get; private set; }

    /// <summary>Trust's case identifier. How an outcome finds its way back to this row.</summary>
    public Guid? VerificationId { get; private set; }

    /// <summary>Trust's badge label. Rendered, never interpreted.</summary>
    public string? Badge { get; private set; }

    /// <summary>Why Trust refused or withdrew. Shown to the owner, never to buyers.</summary>
    public string? Reason { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>When the state last moved. The staleness guard compares against this.</summary>
    public DateTimeOffset? ChangedAt { get; private set; }

    /// <summary>Never claimed.</summary>
    public static ListingTrustMark None =>
        new(ListingVerificationState.None, null, null, null, null, null);

    public bool IsVerified => State == ListingVerificationState.Verified;

    /// <summary>Whether a decision that arrived is older than the one already applied.</summary>
    public bool IsNewerThan(DateTimeOffset decidedAt) =>
        ChangedAt is { } changed && changed > decidedAt;

    public static ListingTrustMark Pending(DateTimeOffset now, Guid? verificationId) =>
        new(ListingVerificationState.Pending, verificationId, null, null, null, now);

    /// <summary>Reachable only from a handler for a Trust integration event.</summary>
    public static ListingTrustMark Verified(
        Guid? verificationId, string? badge, DateTimeOffset decidedAt) =>
        new(ListingVerificationState.Verified,
            verificationId,
            Clamp(badge, MarketplacePersistenceLimits.Badge),
            null,
            decidedAt,
            decidedAt);

    public static ListingTrustMark Rejected(
        Guid? verificationId, string? reason, DateTimeOffset decidedAt) =>
        new(ListingVerificationState.Rejected,
            verificationId,
            null,
            Clamp(reason, MarketplacePersistenceLimits.Reason),
            null,
            decidedAt);

    public static ListingTrustMark Revoked(
        Guid? verificationId, string? reason, DateTimeOffset decidedAt) =>
        new(ListingVerificationState.Revoked,
            verificationId,
            null,
            Clamp(reason, MarketplacePersistenceLimits.Reason),
            null,
            decidedAt);

    public static ListingTrustMark Cleared(string? reason, DateTimeOffset decidedAt) =>
        new(ListingVerificationState.None,
            null,
            null,
            Clamp(reason, MarketplacePersistenceLimits.Reason),
            null,
            decidedAt);

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
        yield return Reason;
        yield return VerifiedAt;
        yield return ChangedAt;
    }
}
