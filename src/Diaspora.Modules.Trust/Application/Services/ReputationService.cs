using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Applies scoring rules to a member, enforcing the caps that make reputation mean something.
/// <para>
/// The caps are the reason this is a service rather than a call straight into the aggregate.
/// <c>MaxOccurrences</c>, <c>DailyCap</c> and <c>CooldownHours</c> need the member's scoring history, which
/// the aggregate does not hold and should not — loading every reputation event a
/// three-year-old account has accumulated in order to award two points for a comment is not
/// a trade worth making.
/// </para>
/// </summary>
public sealed class ReputationService
{
    private readonly IReputationProfileRepository _reputations;
    private readonly ITrustProfileRepository _profiles;
    private readonly ReputationRuleCatalog _rules;
    private readonly IIntegrationEventPublisher _events;
    private readonly IClock _clock;

    public ReputationService(
        IReputationProfileRepository reputations,
        ITrustProfileRepository profiles,
        ReputationRuleCatalog rules,
        IIntegrationEventPublisher events,
        IClock clock)
    {
        _reputations = reputations;
        _profiles = profiles;
        _rules = rules;
        _events = events;
        _clock = clock;
    }

    /// <summary>The policy version a points grant carries: the rules that priced it.</summary>
    public static string PolicyVersion => $"reputation@{DefaultReputationRules.Version}";

    /// <summary>
    /// Scores an action, if the rules allow it.
    /// <para>
    /// Returns <see cref="ScoringOutcome.Skipped"/> rather than throwing when a cap is hit.
    /// A member exceeding their daily comment allowance has not done anything wrong — their
    /// comment still posts, it simply stops earning — and a caller should not have to catch
    /// an exception to express that.
    /// </para>
    /// <para>
    /// <paramref name="key"/> has no default on purpose. Every caller must say which business
    /// fact it is paying for, and a fact already paid is skipped before any cap is consulted.
    /// The unique index on <c>(ProfileId, AwardKey)</c> backs this when two deliveries race: the
    /// loser's save throws, the outbox retries it, and the retry lands here and is skipped.
    /// </para>
    /// </summary>
    public async Task<ScoringOutcome> ScoreAsync(
        UserId userId,
        string actionType,
        ReputationAwardKey key,
        string? sourceType = null,
        string? sourceId = null,
        string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(key);

        var rule = _rules.For(actionType);
        if (rule is null)
        {
            // An unknown action is a caller bug, but not one worth failing a request over —
            // the member's comment should still post if Trust does not recognise the action.
            return ScoringOutcome.Skipped(actionType, "No active scoring rule for this action.");
        }

        if (await _reputations.HasAwardAsync(userId, key, cancellationToken))
        {
            return ScoringOutcome.Skipped(actionType, "Already awarded for this.");
        }

        var now = _clock.UtcNow;

        // A country-scoped rule asks about its own country, so its cap means once *per
        // country*. Counting them together would charge a member's Mauritius residency against
        // the UK they moved to, and pay them nothing for the second — which is an ordinary
        // thing to do now that a move can be recorded.
        var scope = rule.IsCountryScoped ? countryCode : null;

        // A daily cap asks about the last 24 hours, not the calendar day: no time zone to choose, and
        // the same reading the Financial Platform's staff grant ceiling uses.
        DateTimeOffset? dayStart = rule.DailyCap is > 0 ? now.AddDays(-1) : null;

        var usage = await _reputations.GetRuleUsageAsync(
            userId, rule.ActionType, scope, dayStart, cancellationToken);

        var counted = scope is null ? usage.Total : usage.InCountry;

        if (rule.MaxOccurrences is { } max && counted >= max)
        {
            return ScoringOutcome.Skipped(
                actionType, $"This action has already been scored its maximum {max} times.");
        }

        // Global, like the cooldown below and for the same reason.
        if (rule.DailyCap is { } dailyCap and > 0 && usage.AwardedSince >= dailyCap)
        {
            return ScoringOutcome.Skipped(
                actionType, $"This action can only be scored {dailyCap} times a day.");
        }

        // The cooldown stays global on purpose. A cap is about how much a thing is worth; a
        // cooldown is about how fast it can be farmed, and per-country would let the same
        // burst through once for every country on the platform.
        if (rule.CooldownHours is { } cooldown and > 0
            && usage.LastAt is { } last
            && now - last < TimeSpan.FromHours(cooldown))
        {
            return ScoringOutcome.Skipped(
                actionType, $"This action can only be scored once every {cooldown}h.");
        }

        var reputation = await _reputations.GetByUserAsync(userId, cancellationToken);
        if (reputation is null)
        {
            reputation = ReputationProfile.Open(userId, now);
            await _reputations.AddAsync(reputation, cancellationToken);
        }

        var recorded = reputation.Apply(
            rule.ToAward(), sourceType, sourceId, countryCode, now, key);

        // Country-scoped rules also move the member's standing in that community, which is
        // what the per-country reputation the trust centre renders is built from.
        if (rule.IsCountryScoped && !string.IsNullOrWhiteSpace(countryCode))
        {
            var profile = await _profiles.GetByUserAsync(userId, cancellationToken);
            profile?.AddCountryPoints(countryCode, rule.Points, now);
        }

        // A confirmed outcome also pays reward points (D-2, D-12): Trust owns the schedule and says
        // so; the Financial Platform holds the balance. In this unit of work, through the outbox, so
        // the XP and the grant commit together or not at all. The award key is the grant's
        // reference, which makes a redelivered grant pay once at the ledger as well as here.
        if (rule.CreditPoints is > 0 and var points)
        {
            await _events.PublishAsync(
                new RewardPointsGrantedIntegrationEvent
                {
                    MemberUserId = userId.Value,
                    Points = points,
                    Source = actionType,
                    GrantReference = key.Value,
                    GrantedAt = now,
                    PolicyVersion = PolicyVersion
                },
                cancellationToken);
        }

        return ScoringOutcome.Applied(actionType, rule.Points, rule.Dimension, recorded.Id);
    }

    /// <summary>
    /// Applies a handler's approval award, which is already a concrete award rather than a
    /// rule lookup. Used when a verification is granted.
    /// </summary>
    public async Task<ScoringOutcome> ApplyAwardAsync(
        UserId userId,
        ReputationAward award,
        ReputationAwardKey key,
        string? sourceType = null,
        string? sourceId = null,
        string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(award);
        ArgumentNullException.ThrowIfNull(key);

        // Route through the rule when one exists, so caps still apply — verifying the same
        // country twice should not pay twice.
        if (_rules.For(award.ActionType) is not null)
        {
            return await ScoreAsync(
                userId, award.ActionType, key, sourceType, sourceId, countryCode, cancellationToken);
        }

        if (await _reputations.HasAwardAsync(userId, key, cancellationToken))
        {
            return ScoringOutcome.Skipped(award.ActionType, "Already awarded for this.");
        }

        var now = _clock.UtcNow;

        var reputation = await _reputations.GetByUserAsync(userId, cancellationToken);
        if (reputation is null)
        {
            reputation = ReputationProfile.Open(userId, now);
            await _reputations.AddAsync(reputation, cancellationToken);
        }

        var recorded = reputation.Apply(award, sourceType, sourceId, countryCode, now, key);

        return ScoringOutcome.Applied(award.ActionType, award.Points, award.Dimension, recorded.Id);
    }

    /// <summary>
    /// Reverses every unreversed award or penalty with this source (D-9). Staged; the caller saves.
    /// <para>
    /// <b>Found by source, across profiles, not by member.</b> A removal event does not always name
    /// whose content it was (a removed comment's does not), and every award already records what
    /// earned it. <paramref name="which"/> picks the awards (points above zero) or the penalties
    /// (below), and <paramref name="memberUserId"/>, when given, keeps it to one member.
    /// </para>
    /// </summary>
    /// <returns>How many events were reversed. Zero on a second delivery.</returns>
    public async Task<int> ReverseBySourceAsync(
        string? sourceType,
        string sourceId,
        string reason,
        Func<ReputationEvent, bool> which,
        UserId? memberUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(which);

        var originals = await _reputations.ListUnreversedBySourceAsync(
            sourceType, sourceId, cancellationToken);

        return await ReverseAsync(originals.Where(which), reason, memberUserId, cancellationToken);
    }

    /// <summary>
    /// Reverses one member's unreversed events that carry any of these award keys (D-9). Staged; the
    /// caller saves. For a refund whose target is the decision rather than the content.
    /// </summary>
    public async Task<int> ReverseAwardsAsync(
        UserId memberUserId,
        IReadOnlyCollection<ReputationAwardKey> keys,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserId);
        ArgumentNullException.ThrowIfNull(keys);

        var originals = await _reputations.ListUnreversedByAwardKeysAsync(
            memberUserId, [.. keys.Select(key => key.Value)], cancellationToken);

        return await ReverseAsync(originals, reason, memberUserId, cancellationToken);
    }

    private async Task<int> ReverseAsync(
        IEnumerable<ReputationEvent> originals,
        string reason,
        UserId? memberUserId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var reversed = 0;

        foreach (var original in originals)
        {
            var reputation = await _reputations.GetByIdAsync(original.ProfileId, cancellationToken);

            if (reputation is null
                || (memberUserId is not null && reputation.UserId != memberUserId))
            {
                continue;
            }

            // The unique (profile, award key) index backs this when two deliveries race.
            var key = ReputationAwardKey.Reversal(original.Id);

            if (await _reputations.HasAwardAsync(reputation.UserId, key, cancellationToken))
            {
                continue;
            }

            reputation.Reverse(original, reason, now, key);

            // A country-scoped award also moved the member's standing there; that comes back too.
            if (original.CountryCode is { } country
                && _rules.For(original.ActionType) is { IsCountryScoped: true })
            {
                var profile = await _profiles.GetByUserAsync(reputation.UserId, cancellationToken);
                profile?.AddCountryPoints(country, -original.Points, now);
            }

            // The points it paid come back too, if they have not vested (D-9, CS-B3). The Financial
            // Platform decides that; Trust names the grant by the award key it was paid under. A
            // retired rule still names its grant: it stopped paying, and what it paid can still go.
            if (original.AwardKey is { } grantReference && PaidPoints(original.ActionType))
            {
                await _events.PublishAsync(
                    new RewardPointsReversedIntegrationEvent
                    {
                        MemberUserId = reputation.UserId.Value,
                        GrantReference = grantReference,
                        Reason = reason
                    },
                    cancellationToken);
            }

            reversed++;
        }

        return reversed;
    }

    /// <summary>Whether this action's rule pays points, active or retired.</summary>
    private bool PaidPoints(string actionType) =>
        _rules.All.Any(rule =>
            string.Equals(rule.ActionType, actionType, StringComparison.OrdinalIgnoreCase)
            && rule.CreditPoints is > 0);
}

/// <summary>The result of asking to score an action.</summary>
public sealed record ScoringOutcome
{
    public required string ActionType { get; init; }

    public required bool WasApplied { get; init; }

    public int Points { get; init; }

    public ReputationDimension? Dimension { get; init; }

    public ReputationEventId? EventId { get; init; }

    /// <summary>Why it was skipped. Null when applied.</summary>
    public string? SkipReason { get; init; }

    public static ScoringOutcome Applied(
        string actionType, int points, ReputationDimension dimension, ReputationEventId eventId) => new()
        {
            ActionType = actionType,
            WasApplied = true,
            Points = points,
            Dimension = dimension,
            EventId = eventId
        };

    public static ScoringOutcome Skipped(string actionType, string reason) => new()
    {
        ActionType = actionType,
        WasApplied = false,
        SkipReason = reason
    };
}
