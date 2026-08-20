using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// The Trust module's implementation of the platform's <see cref="ITrustService"/>.
/// <para>
/// This is the seam ADR-002 reserved for B3: <em>"<c>ITrustService</c> from Phase B1 remains
/// a read contract — other modules ask 'what is this member's trust level'. B2 does not
/// implement or write through it. B3 owns it."</em> Until now the kernel's
/// <c>DefaultTrustService</c> answered <c>Basic</c> and <c>true</c> to everything, which was
/// the correct placeholder — visibly wrong rather than plausibly wrong.
/// </para>
/// <para>
/// Registered by <c>services.Replace</c> in the module's own <c>ConfigureServices</c>. The
/// kernel explicitly permits that ("modules configure services after the kernel, so they can
/// replace kernel defaults"), and Identity already does the same for
/// <c>IFeatureGateService</c>. No kernel file changes.
/// </para>
/// </summary>
public sealed class TrustEngineService : ITrustService
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IClock _clock;

    public TrustEngineService(ITrustProfileRepository profiles, IClock clock)
    {
        _profiles = profiles;
        _clock = clock;
    }

    /// <summary>
    /// The member's coarse trust level.
    /// <para>
    /// Projects the module's six-rung <see cref="TrustTier"/> onto the kernel's four-value
    /// <see cref="TrustLevel"/>. Consumers get the stable ladder and are spared every rung
    /// Trust invents later — which is the entire reason the kernel enum is separate and
    /// deliberately coarse.
    /// </para>
    /// <para>
    /// A member with no profile is <see cref="TrustLevel.None"/>, not an error. Other modules
    /// call this on paths where "who is this?" is already answered and "how much do we trust
    /// them?" must not throw.
    /// </para>
    /// </summary>
    public async Task<TrustLevel> GetTrustLevelAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        return profile is null ? TrustLevel.None : ToPlatformLevel(profile.Tier);
    }

    /// <summary>
    /// Whether the member may publish.
    /// <para>
    /// Delegates to <see cref="TrustProfile.CanPublish"/> so there is one definition of the
    /// answer rather than one here and another in the eligibility endpoint. A member with no
    /// profile cannot publish — the safe direction for a question whose wrong answer is
    /// unverified content on the platform.
    /// </para>
    /// </summary>
    public async Task<bool> IsPublishingEligibleAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        return profile?.CanPublish() ?? false;
    }

    /// <summary>
    /// How the member's account stands.
    /// <para>
    /// Projects <see cref="AccountHealth"/> onto the kernel's four-value ladder, for the same
    /// reason <see cref="GetTrustLevelAsync"/> projects the tier: a consumer switching on this
    /// module's five-rung health enum would have to be revisited every time this module invented a
    /// sixth. <c>Excellent</c> and <c>Good</c> are one answer to everybody outside Trust — the
    /// distinction between them is a reputation signal, not a restriction.
    /// </para>
    /// <para>
    /// A member with no profile is <see cref="AccountStanding.Unknown"/>, which consumers are
    /// documented not to treat as a refusal. Returning <see cref="AccountStanding.Good"/> would be
    /// asserting something this module has no record of, and returning a restriction would refuse
    /// every member for the window between registering and being provisioned.
    /// </para>
    /// </summary>
    public async Task<AccountStanding> GetAccountStandingAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        return profile is null ? AccountStanding.Unknown : ToPlatformStanding(profile.Health);
    }

    /// <summary>Maps the module's account health onto the kernel's standing.</summary>
    public static AccountStanding ToPlatformStanding(AccountHealth health) => health switch
    {
        AccountHealth.Suspended => AccountStanding.Suspended,
        AccountHealth.Restricted => AccountStanding.Restricted,
        AccountHealth.UnderReview => AccountStanding.UnderReview,
        _ => AccountStanding.Good
    };

    /// <summary>
    /// Maps the module's tier onto the kernel's.
    /// <para>
    /// <see cref="TrustTier.Provisional"/> maps down to <see cref="TrustLevel.Basic"/>, not
    /// across to <see cref="TrustLevel.Verified"/>: a member whose residency permit lapsed
    /// still has an account, but should not be vouching for anything until they re-verify.
    /// </para>
    /// </summary>
    public static TrustLevel ToPlatformLevel(TrustTier tier) => tier switch
    {
        TrustTier.Steward => TrustLevel.Premium,
        TrustTier.Trusted => TrustLevel.Premium,
        TrustTier.Verified => TrustLevel.Verified,
        TrustTier.Provisional => TrustLevel.Basic,
        TrustTier.Basic => TrustLevel.Basic,
        _ => TrustLevel.None
    };
}
