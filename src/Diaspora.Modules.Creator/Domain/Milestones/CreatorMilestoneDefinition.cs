using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Milestones;

/// <summary>
/// A milestone the programme defines. Reference data.
/// <para>
/// <b>Data rather than code, and the register asks for it in those words:</b> "§22: Milestones
/// should be represented as data-driven entities. Do not hardcode every milestone directly into UI
/// components." The same discipline applies on this side: there is no <c>FirstTenGuidesMilestone</c>
/// class anywhere in this module. A milestone is a row, its rules are rows, and adding one is a
/// seed change.
/// </para>
/// <para>
/// <b><see cref="Rules"/> is what makes a milestone weighted, capped and composite.</b> The register
/// is explicit that a client dividing <c>current / target</c> renders visibly incorrect figures,
/// and it is right — a milestone can count a featured guide at three and an ordinary one at one,
/// and can cap what any single action type contributes so that a hundred comments do not buy a
/// publishing milestone. Both live here, both are applied in
/// <c>CreatorMilestoneProgress.Recompute</c>, and neither is expressible as a division.
/// </para>
/// </summary>
public sealed class CreatorMilestoneDefinition : AggregateRoot<CreatorMilestoneId>, IAuditableEntity
{
    private readonly List<MilestoneRule> _rules = [];

    private CreatorMilestoneDefinition(
        CreatorMilestoneId id, string key, string title, string description, long target)
        : base(id)
    {
        Key = key;
        Title = title;
        Description = description;
        Target = target;
    }

    private CreatorMilestoneDefinition() { }

    /// <summary>Stable, unique, and what a seeder updates a milestone by.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? Icon { get; private set; }

    public string? Category { get; private set; }

    /// <summary>The weighted total that completes it. Never zero on an active milestone.</summary>
    public long Target { get; private set; }

    /// <summary>What the numbers count — "guides", "contributions", "hours".</summary>
    public string? Unit { get; private set; }

    /// <summary>
    /// The percentage past which a milestone reads as <c>AlmostComplete</c>. A presentation state,
    /// and therefore decided here rather than by whichever client is drawing the bar.
    /// </summary>
    public int AlmostCompletePercentage { get; private set; } = 80;

    /// <summary>Ordering the platform wants. Never sorted by title on a device.</summary>
    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>When a seasonal milestone stops being claimable. Null for a permanent one.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    // ── The reward ──────────────────────────────────────────────────────────────────────────
    //
    // Points are the only reward this module grants, and it grants them by publishing to B15. A
    // badge and an entitlement are recorded and left for the module that owns them; a money reward
    // is declared here and executed by B15, never by this module.

    public string RewardKind { get; private set; } = "points";

    public int RewardPoints { get; private set; }

    public string? RewardBadgeKey { get; private set; }

    public string? RewardEntitlementKey { get; private set; }

    public string RewardLabel { get; private set; } = string.Empty;

    public IReadOnlyList<MilestoneRule> Rules => _rules.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorMilestoneDefinition Define(
        CreatorMilestoneId id,
        string key,
        string title,
        string description,
        long target,
        string rewardLabel,
        int rewardPoints,
        int sortOrder,
        string? category = null,
        string? icon = null,
        string? unit = null,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new CreatorMilestoneDefinition(id, key.Trim(), title.Trim(), description.Trim(), target)
        {
            Category = category,
            Icon = icon,
            Unit = unit,
            SortOrder = sortOrder,
            RewardLabel = rewardLabel,
            RewardPoints = rewardPoints,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>Adds a counting rule. At least one is required for a milestone to be reachable.</summary>
    public CreatorMilestoneDefinition Counting(string actionType, int weight, long? cap = null)
    {
        _rules.Add(MilestoneRule.Create(actionType, weight, cap));
        return this;
    }

    /// <summary>
    /// Rewrites the definition in place, for a re-run of the seeder.
    /// <para>
    /// The key is the identity, so a re-seed updates rather than duplicates — which is what makes
    /// <c>IReferenceDataSeeder</c>'s idempotency contract true of this catalogue.
    /// </para>
    /// </summary>
    public void Redefine(
        string title,
        string description,
        long target,
        string rewardLabel,
        int rewardPoints,
        int sortOrder,
        string? category,
        string? icon,
        string? unit,
        DateTimeOffset? expiresAt,
        IEnumerable<MilestoneRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        Title = title.Trim();
        Description = description.Trim();
        Target = target;
        RewardLabel = rewardLabel;
        RewardPoints = rewardPoints;
        SortOrder = sortOrder;
        Category = category;
        Icon = icon;
        Unit = unit;
        ExpiresAt = expiresAt;
        IsActive = true;

        _rules.Clear();
        _rules.AddRange(rules);
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Names the badge that claiming this milestone earns. Returns whether anything changed.
    /// <para>
    /// Added 21 Oct 2026: two reward labels had promised a badge since B18 and no definition named
    /// one, so <c>CreatorBadgeReader</c> — which derives a badge from exactly this field — had
    /// served an empty list to every creator.
    /// </para>
    /// </summary>
    public bool AwardBadge(string badgeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(badgeKey);

        var key = badgeKey.Trim();

        if (string.Equals(RewardBadgeKey, key, StringComparison.Ordinal))
        {
            return false;
        }

        RewardBadgeKey = key;

        return true;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}

/// <summary>
/// One counting rule inside a milestone.
/// <para>
/// <b><see cref="Cap"/> is why a division is wrong.</b> "Publish 10 guides and answer 20 questions,
/// but comments count for at most 20 of the 40" is one milestone with two rules and a cap on the
/// second. A client dividing a total by a target would render the same bar whether the cap had bitten
/// or not.
/// </para>
/// </summary>
public sealed class MilestoneRule : ValueObject
{
    private MilestoneRule(string actionType, int weight, long? cap)
    {
        ActionType = actionType;
        Weight = weight;
        Cap = cap;
    }

    private MilestoneRule() { }

    /// <summary>A member of <c>CreatorActivityTypes</c>. Validated when the catalogue is seeded.</summary>
    public string ActionType { get; private set; } = string.Empty;

    /// <summary>What one occurrence is worth toward the target. At least one.</summary>
    public int Weight { get; private set; } = 1;

    /// <summary>The most this rule may contribute, however many times it happens. Null for no cap.</summary>
    public long? Cap { get; private set; }

    public static MilestoneRule Create(string actionType, int weight, long? cap = null) =>
        new(actionType.Trim(), Math.Max(1, weight), cap is > 0 ? cap : null);

    /// <summary>What a raw count is worth under this rule, capped.</summary>
    public long Contribution(long rawCount)
    {
        var weighted = Math.Max(0, rawCount) * Weight;

        return Cap is { } cap ? Math.Min(weighted, cap) : weighted;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ActionType;
        yield return Weight;
        yield return Cap;
    }
}
