using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One scoring event against a member's reputation.
/// <para>
/// Append-only, and the reason the reputation profile's totals are auditable rather than
/// merely present: any score can be reconstructed by summing these, so "why do I have 525
/// points?" has an answer. The frontend already declares the matching
/// <c>ReputationEvent</c> type — it is simply not fetched yet.
/// </para>
/// </summary>
public sealed class ReputationEvent : Entity<ReputationEventId>
{
    public const int MaxActionTypeLength = 64;
    public const int MaxSourceTypeLength = 64;
    public const int MaxSourceIdLength = 128;
    public const int MaxDescriptionLength = 500;

    public ReputationProfileId ProfileId { get; private set; } = default!;

    /// <summary>The scoring rule that fired — "GuidePublished", "HelpfulVoteReceived".</summary>
    public string ActionType { get; private set; } = default!;

    public ReputationDimension Dimension { get; private set; }

    /// <summary>Signed. Violations subtract.</summary>
    public int Points { get; private set; }

    /// <summary>What the points were earned on — "guide", "update", "comment".</summary>
    public string? SourceType { get; private set; }

    public string? SourceId { get; private set; }

    /// <summary>Which country community this counted toward, when it is country-scoped.</summary>
    public string? CountryCode { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// Which business fact this award was for — see <see cref="ReputationAwardKey"/>. Unique per
    /// profile. Null on rows written before awards were keyed, and only on those.
    /// </summary>
    public string? AwardKey { get; private set; }

    /// <summary>
    /// The event this one reverses (D-9): an award taken back, or a penalty refunded. Null on every
    /// award and penalty. A reversal shares the original's action type and source, so the
    /// breakdown and the caps net it against the original rather than counting a second action.
    /// </summary>
    public ReputationEventId? ReversesEventId { get; private set; }

    /// <summary>
    /// What this event actually moved its dimension's score by, after the floor at zero: a 75-point
    /// fine against a score of 10 applies -10. A reversal undoes this, not <see cref="Points"/>,
    /// so it never refunds more than was taken. Null on rows written before it was recorded.
    /// </summary>
    public int? AppliedPoints { get; private set; }

    public bool IsReversal => ReversesEventId is not null;

    /// <summary>
    /// The version of the rules that priced this event (<see cref="DefaultReputationRules.Version"/>).
    /// A reversal carries its original's. Null on an event no rule priced, and on every event written
    /// before the rules were versioned.
    /// </summary>
    public int? RuleSetVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private ReputationEvent() { }

    private ReputationEvent(
        ReputationEventId id,
        ReputationProfileId profileId,
        string actionType,
        ReputationDimension dimension,
        int points,
        DateTimeOffset createdAt) : base(id)
    {
        ProfileId = profileId;
        ActionType = actionType;
        Dimension = dimension;
        Points = points;
        CreatedAt = createdAt;
    }

    internal static ReputationEvent Record(
        ReputationProfileId profileId,
        string actionType,
        ReputationDimension dimension,
        int points,
        string? sourceType,
        string? sourceId,
        string? countryCode,
        string? description,
        DateTimeOffset createdAt,
        ReputationAwardKey? awardKey = null,
        int? appliedPoints = null,
        ReputationEventId? reversesEventId = null,
        int? ruleSetVersion = null)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        var action = Truncate(
            Guard.Against.NullOrWhiteSpace(actionType, nameof(actionType)), MaxActionTypeLength)!;

        return new ReputationEvent(
            ReputationEventId.New(), profileId, action, dimension, points, createdAt)
        {
            SourceType = Truncate(sourceType, MaxSourceTypeLength),
            SourceId = Truncate(sourceId, MaxSourceIdLength),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            Description = Truncate(description, MaxDescriptionLength),

            // Never truncated: a shortened key is a different key. The factory already bounds it.
            AwardKey = awardKey?.Value,
            AppliedPoints = appliedPoints,
            ReversesEventId = reversesEventId,
            RuleSetVersion = ruleSetVersion
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
