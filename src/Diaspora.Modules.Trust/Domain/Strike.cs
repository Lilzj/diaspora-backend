using Diaspora.Contracts.Moderation.V1;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>Identifies a strike.</summary>
public sealed record StrikeId(Guid Value) : StronglyTypedId(Value)
{
    public static StrikeId New() => new(Guid.NewGuid());

    public static StrikeId From(Guid value) => new(value);
}

/// <summary>Where a member stands on the ladder after a decision against them stood.</summary>
public enum StrikeLevel
{
    /// <summary>The first: recorded, nothing blocked.</summary>
    Warning = 0,

    /// <summary>Seven days without posting in that area.</summary>
    First = 1,

    /// <summary>Thirty days without posting in that area.</summary>
    Second = 2,

    /// <summary>An account suspension, which two members of staff must confirm.</summary>
    Third = 3
}

/// <summary>
/// One step on a member's ladder, earned when a removal of their content stood (7 Nov 2026).
/// <para>
/// <b>Replaces "one violation restricts the whole account; three suspend it for good".</b> The owner's
/// ladder is graded and forgives: a warning, then a week without posting in the area where it
/// happened, then a month, then a suspension two staff must confirm. A strike counts for twelve months,
/// and an upheld appeal takes it away.
/// </para>
/// </summary>
public sealed class Strike : Entity<StrikeId>
{
    public const int MaxAreaLength = 32;

    public UserId MemberId { get; private set; } = default!;

    public ModerationItemId CaseId { get; private set; } = default!;

    public string Area { get; private set; } = default!;

    public StrikeLevel Level { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Until when the member may not post in <see cref="Area"/>. Null for a warning or a third strike.</summary>
    public DateTimeOffset? BlockedUntil { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    private Strike() { }

    /// <summary>The next step for a member with <paramref name="activeStrikes"/> strikes still counting.</summary>
    public static Strike Issue(
        UserId memberId,
        ModerationItemId caseId,
        string area,
        int activeStrikes,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(caseId);

        var level = activeStrikes switch
        {
            <= 0 => StrikeLevel.Warning,
            1 => StrikeLevel.First,
            2 => StrikeLevel.Second,
            _ => StrikeLevel.Third
        };

        return new Strike
        {
            Id = StrikeId.New(),
            MemberId = memberId,
            CaseId = caseId,
            Area = string.IsNullOrWhiteSpace(area) ? "general" : area.Trim()[..Math.Min(area.Trim().Length, MaxAreaLength)],
            Level = level,
            IssuedAt = at,
            ExpiresAt = at.Add(ModerationPolicy.StrikeLifetime),
            BlockedUntil = level switch
            {
                StrikeLevel.First => at.Add(ModerationPolicy.FirstStrikeBlock),
                StrikeLevel.Second => at.Add(ModerationPolicy.SecondStrikeBlock),
                _ => null
            }
        };
    }

    public bool IsActive(DateTimeOffset asOf) => RevokedAt is null && ExpiresAt > asOf;

    public bool BlocksPosting(string area, DateTimeOffset asOf) =>
        RevokedAt is null
        && BlockedUntil is { } until && until > asOf
        && string.Equals(Area, area, StringComparison.OrdinalIgnoreCase);

    /// <summary>Taken away, because the appeal against the decision behind it was upheld.</summary>
    public void Revoke(string reason, DateTimeOffset at)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = at;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? "Appeal upheld." : reason.Trim();
    }
}
