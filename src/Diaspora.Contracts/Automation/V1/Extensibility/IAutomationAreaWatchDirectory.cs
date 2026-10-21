namespace Diaspora.Contracts.Automation.V1.Extensibility;

/// <summary>
/// Which areas a member's own <c>location.enteredArea</c> automations watch — 21 Oct 2026.
/// <para>
/// <b>Why a seam, and why this direction.</b> Location (300) resolves the member's foreground ping to
/// an area and decides whether they entered a new one; only Automation (2100) knows which areas
/// anybody's rule is about. Location may not reference a module composed after it, so it asks here,
/// and Automation answers. With no implementation registered nobody watches anything, and Location
/// publishes nothing — a ping is then only a transition remembered.
/// </para>
/// <para>
/// Asked only about the member's own automations: the trigger is personal, so nobody else's rule
/// may decide what is published about this member's movements.
/// </para>
/// </summary>
public interface IAutomationAreaWatchDirectory
{
    Task<AutomationAreaWatch> GetAsync(Guid memberId, CancellationToken cancellationToken = default);
}

/// <summary>What a member's active area automations watch.</summary>
/// <param name="WatchesAnyArea">
/// True when one of them names no area to include — it fires on entering any area.
/// </param>
/// <param name="AreaIds">The catalogue areas the rest name.</param>
public sealed record AutomationAreaWatch(bool WatchesAnyArea, IReadOnlySet<Guid> AreaIds)
{
    public static AutomationAreaWatch Nothing { get; } = new(false, new HashSet<Guid>());

    /// <summary>Whether entering any of <paramref name="entered"/> concerns a rule.</summary>
    public bool Concerns(IEnumerable<Guid> entered)
    {
        ArgumentNullException.ThrowIfNull(entered);
        return WatchesAnyArea || entered.Any(AreaIds.Contains);
    }
}
