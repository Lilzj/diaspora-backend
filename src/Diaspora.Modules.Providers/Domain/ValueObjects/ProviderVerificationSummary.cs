using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// The one-line answer to "is this provider verified", derived from its verification claims.
/// <para>
/// Exists so a discovery query can filter on <c>verifiedOnly</c> with an indexed column instead
/// of a subquery over an owned collection, and so a card can render a badge without loading
/// every claim. The client's whole verification surface today is one string on the card, and
/// this is the field that serves it.
/// </para>
/// <para>
/// <b>Derived, never set directly.</b> The aggregate recomputes it from
/// <c>ProviderVerification</c> whenever a claim changes state; there is no setter, and the
/// only construction path takes the claims. That is what keeps the denormalised flag and the
/// claims it summarises from disagreeing — the failure mode that would show a verified badge on
/// a provider whose registration lapsed six months ago.
/// </para>
/// </summary>
public sealed class ProviderVerificationSummary : ValueObject
{
    private ProviderVerificationSummary(
        bool isVerified,
        ProviderVerificationState state,
        int verifiedKindCount,
        DateTimeOffset? verifiedAt,
        DateTimeOffset? expiresAt)
    {
        IsVerified = isVerified;
        State = state;
        VerifiedKindCount = verifiedKindCount;
        VerifiedAt = verifiedAt;
        ExpiresAt = expiresAt;
    }

    private ProviderVerificationSummary() { }

    /// <summary>The client's <c>verificationStatus === 'Verified'</c>, and the search filter.</summary>
    public bool IsVerified { get; private set; }

    /// <summary>
    /// The most advanced state any claim has reached. Lets a client distinguish "never applied"
    /// from "applied and waiting" from "was verified and lapsed", which one boolean cannot.
    /// </summary>
    public ProviderVerificationState State { get; private set; } = ProviderVerificationState.Unverified;

    /// <summary>How many kinds are currently granted. One input to the default ranking.</summary>
    public int VerifiedKindCount { get; private set; }

    /// <summary>When the earliest still-current grant was made.</summary>
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>The soonest a current grant lapses. What a renewal reminder is scheduled from.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static ProviderVerificationSummary Unverified =>
        new(false, ProviderVerificationState.Unverified, 0, null, null);

    /// <summary>
    /// Recomputes the summary from every claim a provider holds.
    /// <para>
    /// A claim counts as granted only when it is <see cref="ProviderVerificationState.Verified"/>
    /// <em>and</em> not past its own expiry as at <paramref name="asAt"/>. Checking expiry here
    /// rather than trusting the stored state is what makes the badge correct on the day a grant
    /// lapses instead of on the day a sweeper next runs.
    /// </para>
    /// </summary>
    public static ProviderVerificationSummary From(
        IEnumerable<(ProviderVerificationState State, DateTimeOffset? GrantedAt, DateTimeOffset? ExpiresAt)> claims,
        DateTimeOffset asAt)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var granted = 0;
        DateTimeOffset? earliestGrant = null;
        DateTimeOffset? soonestExpiry = null;
        var best = ProviderVerificationState.Unverified;

        foreach (var claim in claims)
        {
            best = Rank(claim.State) > Rank(best) ? claim.State : best;

            var isCurrent = claim.State == ProviderVerificationState.Verified
                && (claim.ExpiresAt is null || claim.ExpiresAt > asAt);

            if (!isCurrent)
            {
                continue;
            }

            granted++;

            if (claim.GrantedAt is { } grantedAt && (earliestGrant is null || grantedAt < earliestGrant))
            {
                earliestGrant = grantedAt;
            }

            if (claim.ExpiresAt is { } expiry && (soonestExpiry is null || expiry < soonestExpiry))
            {
                soonestExpiry = expiry;
            }
        }

        var isVerified = granted > 0;

        return new ProviderVerificationSummary(
            isVerified,
            isVerified ? ProviderVerificationState.Verified : best,
            granted,
            earliestGrant,
            soonestExpiry);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsVerified;
        yield return State;
        yield return VerifiedKindCount;
        yield return VerifiedAt;
        yield return ExpiresAt;
    }

    /// <summary>
    /// How informative a state is when several disagree. Verified beats pending beats a past
    /// failure beats never having tried — which is the order a member reads them in.
    /// </summary>
    private static int Rank(ProviderVerificationState state) => state switch
    {
        ProviderVerificationState.Verified => 5,
        ProviderVerificationState.Pending => 4,
        ProviderVerificationState.Expired => 3,
        ProviderVerificationState.Revoked => 2,
        ProviderVerificationState.Rejected => 1,
        _ => 0
    };
}
