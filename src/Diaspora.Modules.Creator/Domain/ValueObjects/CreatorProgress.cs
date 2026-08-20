namespace Diaspora.Modules.Creator.Domain.ValueObjects;

/// <summary>
/// A progress figure the server computed.
/// <para>
/// <b><see cref="Percentage"/> is computed here and never by dividing on a device, and the register
/// says why:</b> "Weighted and capped milestones make <c>current / target</c> wrong, and the
/// fixture deliberately contains two that are, so a client that divided renders visibly incorrect
/// figures in development." A milestone can weight a featured guide at three and an ordinary one at
/// one, and can cap what any single week contributes; the arithmetic that survives both is this
/// one, in one place.
/// </para>
/// <para>
/// Not an EF-mapped type. It is derived from columns the aggregate does persist, so there is no
/// stored percentage to fall out of step with the counters behind it.
/// </para>
/// </summary>
public readonly record struct CreatorProgress
{
    private CreatorProgress(long current, long target, int percentage, string? unit)
    {
        Current = current;
        Target = target;
        Percentage = percentage;
        Unit = unit;
    }

    /// <summary>What the creator has, in the milestone's own units. Weighted where it weights.</summary>
    public long Current { get; }

    /// <summary>What the milestone asks for. Never zero on a served figure.</summary>
    public long Target { get; }

    /// <summary>0–100, clamped. Never above 100, however far past the target the creator is.</summary>
    public int Percentage { get; }

    /// <summary>"guides", "points", "hours" — what the numbers count. Optional.</summary>
    public string? Unit { get; }

    /// <summary>
    /// Builds a progress figure, clamping both ends.
    /// <para>
    /// A target of zero is a configuration mistake rather than a completed milestone, so it reads
    /// as 0% rather than 100%: a milestone nobody can fail is worse than one nobody can see.
    /// </para>
    /// </summary>
    public static CreatorProgress Of(long current, long target, string? unit = null)
    {
        var safeCurrent = Math.Max(0, current);

        if (target <= 0)
        {
            return new CreatorProgress(safeCurrent, 0, 0, unit);
        }

        var capped = Math.Min(safeCurrent, target);
        var percentage = (int)Math.Round(capped * 100d / target, MidpointRounding.AwayFromZero);

        return new CreatorProgress(safeCurrent, target, Math.Clamp(percentage, 0, 100), unit);
    }

    /// <summary>Whether the target is reached. The one place "complete" is decided arithmetically.</summary>
    public bool IsComplete => Target > 0 && Current >= Target;
}

/// <summary>
/// One requirement toward a status, a verification, a challenge or a grant.
/// <para>
/// <b><see cref="IsMet"/> is decided here.</b> The register: "A client that decided '3 of 5 guides,
/// so not met' would be re-implementing an eligibility rule it does not own — and would disagree
/// with the server the first time the rule changed."
/// </para>
/// <para>
/// <b>Requirements are computed, never stored.</b> A stored requirement is a snapshot of a rule,
/// and the rule is the thing that changes. What is stored is the counters; what is served is this,
/// derived on every read against the rule as it is today.
/// </para>
/// </summary>
public sealed record CreatorRequirement
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public required bool IsMet { get; init; }

    /// <summary>Where the member goes to satisfy it, when there is somewhere.</summary>
    public string? ActionRoute { get; init; }

    public string? ActionLabel { get; init; }

    /// <summary>Progress toward it, when the requirement is countable.</summary>
    public long? Current { get; init; }

    public long? Target { get; init; }

    /// <summary>A countable requirement, with its own progress.</summary>
    public static CreatorRequirement Counted(
        string key,
        string label,
        long current,
        long target,
        string? description = null,
        string? actionRoute = null,
        string? actionLabel = null) =>
        new()
        {
            Key = key,
            Label = label,
            Description = description,
            IsMet = target <= 0 || current >= target,
            Current = current,
            Target = target,
            ActionRoute = actionRoute,
            ActionLabel = actionLabel
        };

    /// <summary>A requirement that is simply true or not.</summary>
    public static CreatorRequirement Flag(
        string key,
        string label,
        bool isMet,
        string? description = null,
        string? actionRoute = null,
        string? actionLabel = null) =>
        new()
        {
            Key = key,
            Label = label,
            Description = description,
            IsMet = isMet,
            ActionRoute = actionRoute,
            ActionLabel = actionLabel
        };
}
