using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Applies scoring rules to a member, enforcing the caps that make reputation mean something.
/// <para>
/// The caps are the reason this is a service rather than a call straight into the aggregate.
/// <c>MaxOccurrences</c> and <c>CooldownHours</c> need the member's scoring history, which
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
    private readonly IClock _clock;

    public ReputationService(
        IReputationProfileRepository reputations,
        ITrustProfileRepository profiles,
        ReputationRuleCatalog rules,
        IClock clock)
    {
        _reputations = reputations;
        _profiles = profiles;
        _rules = rules;
        _clock = clock;
    }

    /// <summary>
    /// Scores an action, if the rules allow it.
    /// <para>
    /// Returns <see cref="ScoringOutcome.Skipped"/> rather than throwing when a cap is hit.
    /// A member exceeding their daily comment allowance has not done anything wrong — their
    /// comment still posts, it simply stops earning — and a caller should not have to catch
    /// an exception to express that.
    /// </para>
    /// </summary>
    public async Task<ScoringOutcome> ScoreAsync(
        UserId userId,
        string actionType,
        string? sourceType = null,
        string? sourceId = null,
        string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var rule = _rules.For(actionType);
        if (rule is null)
        {
            // An unknown action is a caller bug, but not one worth failing a request over —
            // the member's comment should still post if Trust does not recognise the action.
            return ScoringOutcome.Skipped(actionType, "No active scoring rule for this action.");
        }

        var now = _clock.UtcNow;

        var (usageCount, lastAt) = await _reputations.GetRuleUsageAsync(
            userId, rule.ActionType, cancellationToken);

        if (rule.MaxOccurrences is { } max && usageCount >= max)
        {
            return ScoringOutcome.Skipped(
                actionType, $"This action has already been scored its maximum {max} times.");
        }

        if (rule.CooldownHours is { } cooldown and > 0
            && lastAt is { } last
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
            rule.ToAward(), sourceType, sourceId, countryCode, now);

        // Country-scoped rules also move the member's standing in that community, which is
        // what the per-country reputation the trust centre renders is built from.
        if (rule.IsCountryScoped && !string.IsNullOrWhiteSpace(countryCode))
        {
            var profile = await _profiles.GetByUserAsync(userId, cancellationToken);
            profile?.AddCountryPoints(countryCode, rule.Points, now);
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
        string? sourceType = null,
        string? sourceId = null,
        string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(award);

        // Route through the rule when one exists, so caps still apply — verifying the same
        // country twice should not pay twice.
        if (_rules.For(award.ActionType) is not null)
        {
            return await ScoreAsync(
                userId, award.ActionType, sourceType, sourceId, countryCode, cancellationToken);
        }

        var now = _clock.UtcNow;

        var reputation = await _reputations.GetByUserAsync(userId, cancellationToken);
        if (reputation is null)
        {
            reputation = ReputationProfile.Open(userId, now);
            await _reputations.AddAsync(reputation, cancellationToken);
        }

        var recorded = reputation.Apply(award, sourceType, sourceId, countryCode, now);

        return ScoringOutcome.Applied(award.ActionType, award.Points, award.Dimension, recorded.Id);
    }
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
