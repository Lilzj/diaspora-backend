using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Activity;

/// <summary>
/// One recognised action by one creator.
/// <para>
/// <b>This ledger is what makes four other gaps answerable, and it is not analytics.</b> An entry
/// says "this creator published a guide on the third, and the programme counts it as three". It
/// never says how many people read it. CREATOR-GAP-007 — views, reach, engagement rate — belongs to
/// B19, and §21 of the phase brief forbids inventing those figures from anything here.
/// </para>
/// <para>
/// What the ledger buys, and why a running counter alone would not do:
/// </para>
/// <list type="bullet">
///   <item>A milestone can be <b>weighted and capped</b>, because the raw occurrences survive.</item>
///   <item>A challenge task can count only what happened <b>after the member joined</b>.</item>
///   <item>A referral's qualification rule can ask what the invitee has actually <b>done</b>.</item>
///   <item>
///     A leaderboard can answer <b>for a period</b>. The register asks for a period on the ranking,
///     and a lifetime total with a period label on it would be the exact failure CREATOR-GAP-007
///     records for analytics.
///   </item>
/// </list>
/// <para>
/// <b><see cref="SourceReference"/> is the idempotency barrier.</b> Delivery is at-least-once, so a
/// redelivered <c>GuidePublishedIntegrationEvent</c> must not count twice. The reference is the
/// producing event's own subject — "guide.published:{guideId}" — and it is uniquely indexed per
/// creator, so two workers racing one delivery lose one at the database rather than both counting.
/// </para>
/// </summary>
public sealed class CreatorActivityEntry : AggregateRoot<CreatorActivityEntryId>
{
    private CreatorActivityEntry(
        CreatorActivityEntryId id,
        CreatorId creatorId,
        Guid userId,
        string actionType,
        int weight,
        string sourceReference,
        DateTimeOffset occurredAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        ActionType = actionType;
        Weight = weight;
        SourceReference = sourceReference;
        OccurredAt = occurredAt;
    }

    private CreatorActivityEntry() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    /// <summary>A member of <c>CreatorActivityTypes</c>.</summary>
    public string ActionType { get; private set; } = string.Empty;

    /// <summary>What the programme counts this occurrence as. At least one.</summary>
    public int Weight { get; private set; } = 1;

    /// <summary>What it was about — "guide", "event", "listing". Never resolved by this module.</summary>
    public string? SubjectType { get; private set; }

    public Guid? SubjectId { get; private set; }

    /// <summary>
    /// Where the creator was, denormalised so a country leaderboard is one index rather than a join
    /// to a profile whose country may since have changed. A ranking for March should rank March.
    /// </summary>
    public string? CountryCode { get; private set; }

    /// <summary>
    /// The community the action happened in, where it happened in one. The community leaderboard's
    /// filter, and null for everything that happened outside an organization.
    /// </summary>
    public Guid? CommunityId { get; private set; }

    /// <summary>The producing event's own handle. Unique per creator — see the class summary.</summary>
    public string SourceReference { get; private set; } = string.Empty;

    /// <summary>
    /// When the action happened, not when it was recorded. The period leaderboard's sort key, and
    /// the challenge window's comparison.
    /// </summary>
    public DateTimeOffset OccurredAt { get; private set; }

    public static CreatorActivityEntry Record(
        CreatorActivityEntryId id,
        CreatorId creatorId,
        Guid userId,
        string actionType,
        int weight,
        string sourceReference,
        DateTimeOffset occurredAt,
        string? subjectType = null,
        Guid? subjectId = null,
        string? countryCode = null,
        Guid? communityId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);

        return new CreatorActivityEntry(
            id,
            creatorId,
            userId,
            actionType.Trim(),
            Math.Max(1, weight),
            sourceReference.Trim(),
            occurredAt)
        {
            SubjectType = subjectType?.Trim(),
            SubjectId = subjectId,
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            CommunityId = communityId
        };
    }
}
