using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Infrastructure.Trust;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Applies a Trust decision to whichever provider raised the claim.
/// <para>
/// <b>The only path to a verified badge in this module.</b> Nothing an owner, a manager or a
/// staff member can do through B7's API reaches <c>Provider.ApplyVerificationOutcome</c>; the
/// four Trust event handlers call this, and nothing else does. ADR-002's separation, applied to
/// the most consequential badge this platform renders.
/// </para>
/// <para>
/// <b>Matching is by Trust's own verification identifier</b>, which B7 stored when it submitted.
/// That is what lets an outcome land on the right claim even for a provider whose submission
/// this module never saw — after a replay, or a deployment gap, or a claim raised before an
/// index was rebuilt.
/// </para>
/// <para>
/// A decision about a verification type that has nothing to do with providers — phone
/// possession, email possession, diaspora eligibility — returns silently. That is the common
/// case, not an error: treating it as one would log several times a minute in a healthy system
/// and train everybody to ignore the log.
/// </para>
/// </summary>
public sealed class TrustVerificationProjector
{
    private readonly IProviderRepository _providers;
    private readonly IGuidGenerator _guids;
    private readonly ILogger<TrustVerificationProjector> _logger;

    public TrustVerificationProjector(
        IProviderRepository providers,
        IGuidGenerator guids,
        ILogger<TrustVerificationProjector> logger)
    {
        _providers = providers;
        _guids = guids;
        _logger = logger;
    }

    public async Task ApplyAsync(
        Guid userId,
        Guid trustVerificationId,
        string verificationType,
        ProviderVerificationState outcome,
        DateTimeOffset decidedAt,
        DateTimeOffset? expiresAt = null,
        string? reasonCode = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var kind = ProviderVerificationTypeMap.FromTrust(verificationType);

        if (kind is null)
        {
            return;
        }

        // Trust reviews people, not rows, so its event names a member. The claim belongs to
        // whichever of that member's providers carries Trust's identifier.
        var candidates = await _providers.ListForMemberAsync(
            UserId.From(userId), ProviderLimits.MaxProvidersPerOwner, cancellationToken);

        var matched = 0;

        foreach (var candidate in candidates)
        {
            var provider = await _providers.GetAsync(candidate.Id, cancellationToken);

            if (provider is null
                || !provider.Verifications.Any(
                    claim => claim.TrustVerificationId == trustVerificationId))
            {
                continue;
            }

            var applied = provider.ApplyVerificationOutcome(
                kind.Value,
                outcome,
                decidedAt,
                expiresAt,
                reasonCode,
                reason,
                trustVerificationId,
                ProviderVerificationId.From(_guids.NewGuid()));

            if (applied.IsSuccess)
            {
                matched++;
            }
        }

        if (matched == 0)
        {
            // Trust decided something about a provider verification and B7 holds no claim for
            // it. Worth a warning: it means either a submission this module never recorded, or a
            // provider deleted between submission and decision.
            _logger.LogWarning(
                "Trust verification {TrustVerificationId} ({Type}) for member {UserId} matched no provider claim.",
                trustVerificationId,
                verificationType,
                userId);
        }
    }
}
