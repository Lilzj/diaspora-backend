using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Rewards;

/// <summary>
/// The catalogue's version, and what the platform says about points expiring.
/// <para>
/// <b>The version is served with every catalogue read, and the register asks for it:</b> "A version
/// or <c>updatedAt</c> on the catalogue, so a changed award value is visible rather than silently
/// different."
/// </para>
/// <para>
/// <b>The expiry policy is a stated answer rather than a silence.</b> §14 of the phase brief: "The
/// backend must explicitly define whether points expire, never expire, expire by source, or expire
/// after a period. If expiration exists, expose it in the contract." The platform's answer today is
/// <see cref="PointsExpiryPolicy.Never"/> — B15's <c>RewardAccount</c> has no expiry mechanism and
/// inventing one here would be a second points policy — and saying so is what makes it an answer.
/// </para>
/// </summary>
public static class CreatorRewardCatalogueVersion
{
    /// <summary>Bumped whenever a seeded rule or reward changes. Served on both catalogue reads.</summary>
    public const string Current = "creator-rewards/1";

    /// <summary>See the class summary. Served with the rules catalogue.</summary>
    public const PointsExpiryPolicy ExpiryPolicy = PointsExpiryPolicy.Never;

    /// <summary>The platform's own sentence about expiry, so the app ships no copy of its own.</summary>
    public const string ExpiryDescription =
        "Points you earn do not expire. They stay on your account until you spend or convert them.";
}

/// <summary>
/// A rule saying what an action pays. Reference data.
/// <para>
/// <b>This is the catalogue CREATOR-GAP-011 asks for, and it is deliberately not the reputation
/// engine's <c>ScoringRule</c>.</b> The register notes the two are "very nearly the contract
/// needed", and §14 of the phase brief adds the warning: "DO NOT assume reputation scoring and
/// creator rewards are identical." They are not. A reputation score measures standing and is never
/// spent; a creator point is a currency the member exchanges. Sharing a table would have meant that
/// changing what a guide is worth in points also changed what it was worth to somebody's standing.
/// </para>
/// <para>
/// <b>The cap and the cooldown are enforced, not decorative.</b> <see cref="MaxOccurrences"/> and
/// <see cref="CooldownHours"/> are read by the award path against the recognised-activity ledger,
/// which is what stops a hundred comments in an hour buying a hundred awards.
/// </para>
/// </summary>
public sealed class CreatorPointRule : AggregateRoot<CreatorPointRuleId>, IAuditableEntity
{
    private CreatorPointRule(
        CreatorPointRuleId id, string key, string actionType, string label, string description, int points)
        : base(id)
    {
        Key = key;
        ActionType = actionType;
        Label = label;
        Description = description;
        Points = points;
    }

    private CreatorPointRule() { }

    /// <summary>Stable and unique. What the client links a point entry back to.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>A member of <c>CreatorActivityTypes</c>. Validated when the catalogue is seeded.</summary>
    public string ActionType { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// What one occurrence weighs toward this programme's leaderboards, milestones and challenges.
    /// The server's number, and never the client's.
    /// <para>
    /// <b>Not reward points (RWD-02, D-12).</b> Nothing pays this value: reward points are paid for
    /// confirmed outcomes by Trust's rules (<c>creditPoints</c> on <c>GET /trust/reputation/rules</c>)
    /// and by this programme's milestones, challenges and sign-up bonus. The name is the contract's,
    /// kept so existing clients still parse.
    /// </para>
    /// </summary>
    public int Points { get; private set; }

    /// <summary>Where the member goes to do it.</summary>
    public string? ActionRoute { get; private set; }

    /// <summary>The cap in words — "up to 5 times a month". Served alongside the numbers.</summary>
    public string? LimitLabel { get; private set; }

    public string? Category { get; private set; }

    /// <summary>How many times this rule may pay, per member, in the cooldown window. Null for no cap.</summary>
    public int? MaxOccurrences { get; private set; }

    /// <summary>The window the cap applies over, and the minimum gap between awards. Null for none.</summary>
    public int? CooldownHours { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorPointRule Define(
        CreatorPointRuleId id,
        string key,
        string actionType,
        string label,
        string description,
        int points,
        int sortOrder,
        string? actionRoute = null,
        string? limitLabel = null,
        string? category = null,
        int? maxOccurrences = null,
        int? cooldownHours = null) =>
        new(id, key.Trim(), actionType.Trim(), label.Trim(), description.Trim(), points)
        {
            SortOrder = sortOrder,
            ActionRoute = actionRoute,
            LimitLabel = limitLabel,
            Category = category,
            MaxOccurrences = maxOccurrences,
            CooldownHours = cooldownHours
        };

    public void Redefine(
        string actionType,
        string label,
        string description,
        int points,
        int sortOrder,
        string? actionRoute,
        string? limitLabel,
        string? category,
        int? maxOccurrences,
        int? cooldownHours)
    {
        ActionType = actionType.Trim();
        Label = label.Trim();
        Description = description.Trim();
        Points = points;
        SortOrder = sortOrder;
        ActionRoute = actionRoute;
        LimitLabel = limitLabel;
        Category = category;
        MaxOccurrences = maxOccurrences;
        CooldownHours = cooldownHours;
        IsActive = true;
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
/// Something points can be exchanged for. Reference data.
/// <para>
/// <b>Distinct from conversion-to-money, which is B15's and goes nowhere near this type.</b> A
/// redemption exchanges points for an entitlement, a promotion code or a placement — a thing the
/// platform grants — and the crossing into currency remains B15's single
/// <c>RewardAccount.Convert</c>.
/// </para>
/// <para>
/// <b><see cref="PointsCost"/> is the authority.</b> §31 of the phase brief: "A malicious client
/// must not be able to POST /rewards/redemptions with points = 999999 and influence the outcome.
/// The server must load the authoritative reward catalogue." The redemption request carries a
/// reward identifier and an idempotency key, and no figure at all.
/// </para>
/// </summary>
public sealed class CreatorRewardDefinition : AggregateRoot<CreatorRewardId>, IAuditableEntity
{
    private CreatorRewardDefinition(
        CreatorRewardId id, string key, string title, string description, int pointsCost)
        : base(id)
    {
        Key = key;
        Title = title;
        Description = description;
        PointsCost = pointsCost;
    }

    private CreatorRewardDefinition() { }

    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>What it costs, in points. The only figure that decides a redemption's price.</summary>
    public int PointsCost { get; private set; }

    public string? Category { get; private set; }

    public string? Icon { get; private set; }

    /// <summary>What the member gets — an entitlement key, a promotion code, a placement.</summary>
    public string? GrantKind { get; private set; }

    /// <summary>The lowest creator status that may redeem this. The locked reason's source.</summary>
    public CreatorStatus MinimumStatus { get; private set; } = CreatorStatus.NotACreator;

    /// <summary>How many of these one creator may ever have. Null for unlimited.</summary>
    public int? MaxPerCreator { get; private set; }

    /// <summary>The minimum gap between two redemptions of this reward by one creator.</summary>
    public int? CooldownHours { get; private set; }

    public DateTimeOffset? AvailableFrom { get; private set; }

    public DateTimeOffset? AvailableUntil { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorRewardDefinition Define(
        CreatorRewardId id,
        string key,
        string title,
        string description,
        int pointsCost,
        int sortOrder,
        string? category = null,
        string? icon = null,
        string? grantKind = null,
        CreatorStatus minimumStatus = CreatorStatus.NotACreator,
        int? maxPerCreator = null,
        int? cooldownHours = null,
        DateTimeOffset? availableFrom = null,
        DateTimeOffset? availableUntil = null) =>
        new(id, key.Trim(), title.Trim(), description.Trim(), pointsCost)
        {
            SortOrder = sortOrder,
            Category = category,
            Icon = icon,
            GrantKind = grantKind,
            MinimumStatus = minimumStatus,
            MaxPerCreator = maxPerCreator,
            CooldownHours = cooldownHours,
            AvailableFrom = availableFrom,
            AvailableUntil = availableUntil
        };

    public void Redefine(
        string title,
        string description,
        int pointsCost,
        int sortOrder,
        string? category,
        string? icon,
        string? grantKind,
        CreatorStatus minimumStatus,
        int? maxPerCreator,
        int? cooldownHours,
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil)
    {
        Title = title.Trim();
        Description = description.Trim();
        PointsCost = pointsCost;
        SortOrder = sortOrder;
        Category = category;
        Icon = icon;
        GrantKind = grantKind;
        MinimumStatus = minimumStatus;
        MaxPerCreator = maxPerCreator;
        CooldownHours = cooldownHours;
        AvailableFrom = availableFrom;
        AvailableUntil = availableUntil;
        IsActive = true;
    }

    /// <summary>Whether this reward is on offer at all right now, ignoring who is asking.</summary>
    public bool IsOnOffer(DateTimeOffset now) =>
        IsActive
        && (AvailableFrom is not { } from || now >= from)
        && (AvailableUntil is not { } until || now <= until);

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
/// One exchange that happened.
/// <para>
/// <b>The points figure on this row is the catalogue's, copied at the moment of redemption.</b> A
/// reward whose price later changes does not retroactively change what somebody paid, which is why
/// the cost is stored here rather than read through to the definition.
/// </para>
/// <para>
/// <b><see cref="CatalogueVersion"/> is stored so a dispute is answerable.</b> "I redeemed this for
/// 300" is a claim the platform can check.
/// </para>
/// </summary>
public sealed class CreatorRewardRedemption
    : AggregateRoot<CreatorRewardRedemptionId>, IAuditableEntity
{
    private CreatorRewardRedemption(
        CreatorRewardRedemptionId id,
        CreatorRewardId rewardId,
        string rewardKey,
        string rewardTitle,
        CreatorId creatorId,
        Guid userId,
        int pointsSpent,
        string catalogueVersion,
        DateTimeOffset createdAt) : base(id)
    {
        RewardId = rewardId;
        RewardKey = rewardKey;
        RewardTitle = rewardTitle;
        CreatorId = creatorId;
        UserId = userId;
        PointsSpent = pointsSpent;
        CatalogueVersion = catalogueVersion;
        State = CreatorRewardState.Pending;
        CreatedAt = createdAt;
    }

    private CreatorRewardRedemption() { }

    public CreatorRewardId RewardId { get; private set; } = default!;

    public string RewardKey { get; private set; } = string.Empty;

    public string RewardTitle { get; private set; } = string.Empty;

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    /// <summary>What it cost, at the moment it was redeemed. See the class summary.</summary>
    public int PointsSpent { get; private set; }

    public string CatalogueVersion { get; private set; } = string.Empty;

    public CreatorRewardState State { get; private set; }

    /// <summary>What the platform issued, when it issued something the member can use.</summary>
    public string? GrantReference { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>
    /// The debit's reference at B15, so the movement on the points ledger and this row can be
    /// reconciled. Set in the same transaction as the debit.
    /// </summary>
    public string? PointsDebitReference { get; private set; }

    /// <summary>Uniquely indexed. Two redemptions carrying one key spend points once.</summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorRewardRedemption Open(
        CreatorRewardRedemptionId id,
        CreatorRewardDefinition reward,
        CreatorId creatorId,
        Guid userId,
        string debitReference,
        string? idempotencyKey,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(reward);

        return new CreatorRewardRedemption(
            id,
            reward.Id,
            reward.Key,
            reward.Title,
            creatorId,
            userId,
            reward.PointsCost,
            CreatorRewardCatalogueVersion.Current,
            createdAt)
        {
            PointsDebitReference = debitReference,
            IdempotencyKey = idempotencyKey
        };
    }

    /// <summary>The platform issued what was bought.</summary>
    public void Fulfil(string? grantReference, DateTimeOffset now)
    {
        if (State != CreatorRewardState.Pending)
        {
            return;
        }

        State = CreatorRewardState.Redeemed;
        GrantReference = string.IsNullOrWhiteSpace(grantReference) ? null : grantReference.Trim();
        CompletedAt = now;
        Touch(now);
    }

    /// <summary>
    /// The platform could not issue it.
    /// <para>
    /// Refunding the points is the caller's responsibility and is done in the same transaction —
    /// this row records the refusal, and a rejection with the points still gone would be the worst
    /// possible outcome of a redemption.
    /// </para>
    /// </summary>
    public void Reject(string reason, DateTimeOffset now)
    {
        if (State != CreatorRewardState.Pending)
        {
            return;
        }

        State = CreatorRewardState.Rejected;
        RejectionReason = reason.Trim();
        CompletedAt = now;
        Touch(now);
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

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
