namespace Diaspora.Contracts.Authorization;

/// <summary>
/// One feature gate, declared by the module that owns the feature.
/// <para>
/// Feature gates and permissions answer different questions. A permission answers "may
/// this request proceed?" and is enforced server-side; a gate answers "should the client
/// show this at all?" and exists so a member sees a locked card with a reason instead of
/// discovering a 403 after filling in a form. A module normally declares both.
/// </para>
/// </summary>
/// <param name="Name">
/// The gate name clients switch on, in <c>module.capability</c> form — for example
/// <c>marketplace.listings.create</c>. Unique across the platform.
/// </param>
/// <param name="Module">The owning module.</param>
/// <param name="Description">Human-readable purpose.</param>
public sealed record FeatureDefinition(string Name, string Module, string Description)
{
    /// <summary>The lowest tier at which the gate is open.</summary>
    public AccessTier MinimumTier { get; init; } = AccessTier.Limited;

    /// <summary>
    /// The highest tier at which the gate is open, for capabilities that stop applying as
    /// the member progresses.
    /// </summary>
    public AccessTier? MaximumTier { get; init; }

    /// <summary>Whether the gate is open only to Diaspora staff roles.</summary>
    public bool StaffOnly { get; init; }

    /// <summary>
    /// Whether the gate stays open under suspension, lock or closure. Almost nothing does.
    /// </summary>
    public bool OpenWhenRestricted { get; init; }
}
