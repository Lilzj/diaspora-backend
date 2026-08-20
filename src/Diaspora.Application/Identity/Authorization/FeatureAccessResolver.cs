using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Authorization;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Turns the platform's gate evaluation into something a member can read.
/// <para>
/// The split matters. The catalogue decides <em>whether</em> a gate is open and returns a
/// reason code; this decides <em>how to say so</em>, in Identity's vocabulary, using
/// Identity's access-state names. Keeping the wording out of the kernel is what will let
/// these strings be localised later without touching platform code — and what stops a
/// Marketplace module from having to phrase Identity's onboarding message.
/// </para>
/// </summary>
public sealed class FeatureAccessResolver : IFeatureAccessResolver
{
    private readonly IFeatureCatalog _catalog;

    public FeatureAccessResolver(IFeatureCatalog catalog)
    {
        _catalog = catalog;
    }

    public FeatureAccessSnapshot Resolve(AccessState accessState, UserRole role)
    {
        ArgumentNullException.ThrowIfNull(accessState);
        ArgumentNullException.ThrowIfNull(role);

        var subject = AccessTierMap.ToSubject(accessState, role);
        var evaluation = _catalog.Evaluate(subject);

        return new FeatureAccessSnapshot
        {
            Granted = evaluation.Granted,
            Blocked = evaluation.Blocked
                .Select(blocked => Describe(blocked, accessState))
                .ToArray()
        };
    }

    public bool IsEnabled(string featureName, AccessState accessState, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(featureName)) return false;

        ArgumentNullException.ThrowIfNull(accessState);
        ArgumentNullException.ThrowIfNull(role);

        // Asks about one gate rather than building the whole snapshot to read one entry
        // from it. This runs on every gate check in every module.
        return _catalog.IsOpen(featureName, AccessTierMap.ToSubject(accessState, role));
    }

    private static FeatureBlockReason Describe(BlockedFeature blocked, AccessState accessState)
    {
        // A member who has not verified their phone gets one message for everything. The
        // catalogue can tell them a gate needs verification; that is true and useless when
        // they have not finished signing up.
        if (!accessState.IsRestricted && !accessState.IsOnboarded)
        {
            return new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = "Verify your phone number to start using Diaspora.",
                UnlockedAt = AccessState.LimitedAccess.Name
            };
        }

        return blocked.Code switch
        {
            FeatureBlockCode.AccountRestricted => new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = RestrictionReason(accessState),
                UnlockedAt = null
            },

            FeatureBlockCode.RequiresOnboarding => new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = "Verify your phone number to start using Diaspora.",
                UnlockedAt = AccessState.LimitedAccess.Name
            },

            FeatureBlockCode.RequiresHigherTier => new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = accessState.IsAwaitingTrustDecision
                    ? "Your verification is being reviewed. This unlocks once it is approved."
                    : "Complete Diaspora verification to unlock this.",
                UnlockedAt = UnlockStateName(blocked.UnlocksAtTier)
            },

            FeatureBlockCode.StaffOnly => new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = "This is available to Diaspora staff only.",
                UnlockedAt = null
            },

            FeatureBlockCode.NoLongerApplicable => new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = blocked.Feature == IdentityFeatures.SubmitVerification
                    ? "A verification request is already in progress."
                    : "This no longer applies to your account.",
                UnlockedAt = null
            },

            _ => new FeatureBlockReason
            {
                Feature = blocked.Feature,
                Reason = "This is not available on your account.",
                UnlockedAt = null
            }
        };
    }

    private static string RestrictionReason(AccessState state)
    {
        if (state == AccessState.Suspended) return "Your account is suspended.";
        if (state == AccessState.Locked) return "Your account is locked. Complete account recovery to restore access.";
        return "Your account has been disabled.";
    }

    /// <summary>
    /// Names the access state a tier corresponds to, so the client sees "Verified" rather
    /// than a platform tier name it has no vocabulary for.
    /// </summary>
    private static string? UnlockStateName(AccessTier? tier) => tier switch
    {
        AccessTier.Onboarding => AccessState.PhoneVerificationPending.Name,
        AccessTier.Limited => AccessState.LimitedAccess.Name,
        AccessTier.PendingVerification => AccessState.VerificationPending.Name,
        AccessTier.Verified => AccessState.Verified.Name,
        _ => null
    };
}
