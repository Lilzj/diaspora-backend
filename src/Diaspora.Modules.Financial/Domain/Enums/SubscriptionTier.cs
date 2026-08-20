namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// The named plan families.
/// <para>
/// A tier is a label on a catalogue row, not a code path. Nothing in this module switches on it;
/// what a plan grants is its entitlements, which is why adding a plan needs no release and no client
/// change.
/// </para>
/// </summary>
public enum SubscriptionTier
{
    Free = 1,
    Premium = 2,
    Professional = 3,
    Organization = 4,
    Provider = 5,

    /// <summary>Reserved. No plan ships at this tier.</summary>
    Enterprise = 6
}
