using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Modules.Recommendations.Extensibility.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.ColdStart;
using Diaspora.Modules.Recommendations.Extensibility.Diversity;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Explainability;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Modules.Recommendations.Infrastructure.Personalization;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Engine;

/// <summary>
/// The one implementation of the one door.
/// <para>
/// <b>Seven steps, in one order, for every surface in the platform.</b> Resolve the member; fetch a
/// pool; apply every policy; score with every configured strategy; explain each result; diversify
/// the page; persist and announce it. Nothing skips a step and there is no second path — which is
/// what makes "blocked content is never recommended" a property of the system rather than a claim
/// about nine call sites.
/// </para>
/// <para>
/// <b>Policies run before ranking, and the order is not an implementation detail.</b> Scoring four
/// hundred candidates and then removing the ones a member blocked would work and would be wrong in
/// two ways: it wastes the expensive step on candidates that were never eligible, and it lets an
/// exclusion be expressed as a low score, which is the collapse of the strategy/policy separation
/// this phase is built on.
/// </para>
/// <para>
/// <b>A strategy that throws is logged and skipped, not fatal.</b> A future adapter calling a model
/// endpoint will fail sometimes, and a member's home screen going blank because a re-ranker timed
/// out is a far worse outcome than a page ranked by the nine strategies that did answer. The
/// diagnostics record which strategies actually contributed, so a silently absent one is visible.
/// </para>
/// <para>
/// <b>The cold-start path replaces retrieval, not ranking.</b> Its candidates still go through every
/// policy and still get explanations — a new member is not exempt from a block, and the reason they
/// are being shown something is exactly the reason worth telling them.
/// </para>
/// </summary>
public sealed class RecommendationEngine : IRecommendationEngine
{
    private const string StrategyCacheKeyPrefix = "recommendations:strategies:";

    private readonly PersonalizationContextFactory _contexts;
    private readonly IEnumerable<IRecommendationCandidateSource> _sources;
    private readonly IRecommendationPolicyEvaluator _policies;
    private readonly IRankingStrategyRepository _strategyCatalog;
    private readonly IRankingStrategyRegistry _strategies;
    private readonly IRecommendationReasonBuilder _reasons;
    private readonly IRecommendationDiversifier _diversifier;
    private readonly IColdStartProvider _coldStart;
    private readonly IRecommendationSetRepository _sets;
    private readonly IRecommendationCandidateRepository _candidates;
    private readonly ISignalRepository _signals;
    private readonly IRecommendationCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly ILogger<RecommendationEngine> _logger;

    public RecommendationEngine(
        PersonalizationContextFactory contexts,
        IEnumerable<IRecommendationCandidateSource> sources,
        IRecommendationPolicyEvaluator policies,
        IRankingStrategyRepository strategyCatalog,
        IRankingStrategyRegistry strategies,
        IRecommendationReasonBuilder reasons,
        IRecommendationDiversifier diversifier,
        IColdStartProvider coldStart,
        IRecommendationSetRepository sets,
        IRecommendationCandidateRepository candidates,
        ISignalRepository signals,
        IRecommendationCache cache,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        IOptions<RecommendationOptions> options,
        ILogger<RecommendationEngine> logger)
    {
        _contexts = contexts;
        _sources = sources;
        _policies = policies;
        _strategyCatalog = strategyCatalog;
        _strategies = strategies;
        _reasons = reasons;
        _diversifier = diversifier;
        _coldStart = coldStart;
        _sets = sets;
        _candidates = candidates;
        _signals = signals;
        _cache = cache;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _options = options;
        _logger = logger;
    }

    public async Task<RecommendationResult> GenerateAsync(
        RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.Value;
        var limit = Math.Clamp(request.Limit, 1, options.MaximumPageSize);

        using var activity = RecommendationTelemetry.Activity.StartActivity(
            "recommendation generation", ActivityKind.Internal);

        activity?.SetTag("recommendation.surface", request.Surface.ToString());

        var context = await _contexts.CreateAsync(
            request.UserId,
            request.Surface,
            request.AccessTier,
            request.TargetType,
            request.CountryCodeOverride,
            cancellationToken);

        // A page the member already has. Three pull-to-refreshes in ten seconds should not be three
        // ranking passes.
        if (!request.ForceRegenerate && !request.Transient &&
            await ReuseAsync(request, context, cancellationToken) is { } reused)
        {
            RecommendationTelemetry.SetsReused.Add(1);
            return reused;
        }

        var stopwatch = Stopwatch.StartNew();

        var pool = context.IsColdStart
            ? await _coldStart.BuildAsync(context, options.CandidatePoolSize, cancellationToken)
            : await FetchPoolAsync(request, context, options, cancellationToken);

        RecommendationTelemetry.CandidatePoolSize.Record(pool.Count);

        if (pool.Count == 0)
        {
            RecommendationTelemetry.EmptyCandidatePools.Add(1);
            RecommendationTelemetry.EmptyGenerations.Add(1);

            return RecommendationResult.Empty(context.IsColdStart);
        }

        var filtered = await _policies.EvaluateAsync(pool, context, cancellationToken);

        if (filtered.Allowed.Count == 0)
        {
            _logger.LogWarning(
                "Every one of {Count} candidates was excluded by policy on the {Surface} surface. "
                + "Exclusions: {Exclusions}.",
                pool.Count,
                request.Surface,
                string.Join(", ", filtered.ExclusionsByPolicy.Select(entry => $"{entry.Key}={entry.Value}")));

            RecommendationTelemetry.EmptyGenerations.Add(1);

            return RecommendationResult.Empty(context.IsColdStart) with
            {
                CandidateCount = pool.Count,
                ExclusionsByPolicy = filtered.ExclusionsByPolicy
            };
        }

        var ranked = await RankAsync(filtered, context, cancellationToken);
        var page = _diversifier.Diversify(ranked, context, limit);

        stopwatch.Stop();

        var strategyKeys = page
            .SelectMany(item => item.Contributions.Select(contribution => contribution.StrategyKey))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var setId = request.Transient || !options.PersistGeneratedSets
            ? null
            : await PersistAsync(request, context, page, pool.Count, filtered, (int)stopwatch.ElapsedMilliseconds, cancellationToken);

        RecordTelemetry(context, page.Count, stopwatch.Elapsed.TotalMilliseconds);

        return new RecommendationResult
        {
            Items = page,
            SetId = setId,
            IsColdStart = context.IsColdStart,
            CandidateCount = pool.Count,
            ExclusionsByPolicy = filtered.ExclusionsByPolicy,
            StrategyKeys = strategyKeys,
            GenerationMilliseconds = (int)stopwatch.ElapsedMilliseconds
        };
    }

    // ── Reuse ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a stored page when one is still fresh.
    /// <para>
    /// Rehydrates the candidates so the caller can render, which is one read against a known set of
    /// identifiers rather than the whole retrieval-and-ranking pass. A stored item whose candidate
    /// has since been withdrawn is dropped — the page is reused, not resurrected.
    /// </para>
    /// </summary>
    private async Task<RecommendationResult?> ReuseAsync(
        RecommendationRequest request,
        PersonalizationContext context,
        CancellationToken cancellationToken)
    {
        var existing = await _sets.FindFreshAsync(
            request.UserId, request.Surface, context.AsOf, cancellationToken);

        if (existing is null || existing.Items.Count == 0)
        {
            return null;
        }

        var targets = existing.Items
            .Select(item => (item.Target.TargetType, item.Target.TargetId))
            .ToList();

        // Read by identity rather than through a candidate source. A source is for discovery, and
        // asking one to find twenty known rows would let a vector adapter answer with something
        // else entirely — which would quietly change what "the page you already have" means.
        var loaded = await _candidates.ListByTargetsAsync(targets, cancellationToken);
        var byTarget = loaded.ToDictionary(candidate => candidate.Target.Key, StringComparer.Ordinal);

        var items = new List<ScoredRecommendation>(existing.Items.Count);

        foreach (var item in existing.Items.OrderBy(entry => entry.Position))
        {
            if (!byTarget.TryGetValue(item.Target.Key, out var candidate) ||
                !candidate.IsRecommendable(context.AsOf))
            {
                continue;
            }

            items.Add(new ScoredRecommendation
            {
                Candidate = candidate,
                Score = item.Score,
                PrimaryStrategyKey = item.PrimaryStrategyKey,
                Explanation = item.Explanation,
                Contributions = item.Contributions
            });
        }

        if (items.Count == 0)
        {
            return null;
        }

        return new RecommendationResult
        {
            Items = items,
            SetId = existing.Id,
            IsColdStart = existing.IsColdStart,
            WasCached = true,
            CandidateCount = existing.CandidateCount,
            ExclusionsByPolicy = new Dictionary<string, int>(StringComparer.Ordinal),
            StrategyKeys = existing.ParseStrategyKeys(),
            GenerationMilliseconds = existing.GenerationMilliseconds
        };
    }

    // ── Retrieval ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Assembles the pool from every source that can serve the request.
    /// <para>
    /// Merged and de-duplicated by target reference, first source wins. Additive rather than
    /// exclusive so a vector adapter can run alongside the projection while a deployment decides
    /// whether to trust it.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<RecommendationCandidate>> FetchPoolAsync(
        RecommendationRequest request,
        PersonalizationContext context,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        var query = new CandidateQuery
        {
            TargetTypes = ResolveTargetTypes(request),
            CountryCode = request.CountryCodeOverride ?? context.EffectiveCountryCode,
            CityName = context.Profile.PrimaryCityName,
            NeighborhoodName = context.Profile.PrimaryNeighborhoodName,
            LanguageCode = null,
            CategoryKeys = request.CategoryKeys,
            MemberTrustLevel = context.Profile.TrustLevel,
            MemberIsVerified = context.Profile.IsVerified,
            Limit = options.CandidatePoolSize,
            AsOf = context.AsOf
        };

        var merged = new Dictionary<string, RecommendationCandidate>(StringComparer.Ordinal);

        foreach (var source in _sources.OrderBy(entry => entry.Order))
        {
            if (!source.CanServe(context))
            {
                continue;
            }

            IReadOnlyList<RecommendationCandidate> fetched;

            try
            {
                fetched = await source.FetchAsync(query, context, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A source that fails is a source that contributes nothing. The others still answer.
                _logger.LogError(
                    exception,
                    "Candidate source {Source} failed on the {Surface} surface and was skipped.",
                    source.Key,
                    request.Surface);

                continue;
            }

            foreach (var candidate in fetched)
            {
                merged.TryAdd(candidate.Target.Key, candidate);
            }
        }

        return merged.Values.ToList();
    }

    private static IReadOnlyList<RecommendationTargetType> ResolveTargetTypes(
        RecommendationRequest request)
    {
        if (request.TargetType is { } single)
        {
            return [single];
        }

        return request.TargetTypes.Count > 0 ? request.TargetTypes : [];
    }

    // ── Ranking ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scores the surviving pool with every configured strategy and explains each result.
    /// <para>
    /// Strategies run over the whole pool in sequence rather than per candidate, which is what lets
    /// each of them normalise within the pool. The per-candidate assembly happens afterwards, once
    /// every strategy has answered.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<ScoredRecommendation>> RankAsync(
        PolicyEvaluationResult filtered,
        PersonalizationContext context,
        CancellationToken cancellationToken)
    {
        var configured = await LoadStrategiesAsync(context.Surface, cancellationToken);

        var applicable = configured
            .Where(configuration => context.TargetType is null ||
                                    configuration.TargetType is null ||
                                    configuration.TargetType == context.TargetType)
            .Where(configuration => !context.IsColdStart || configuration.RunsOnColdStart)
            .OrderBy(configuration => configuration.Order)
            .ToList();

        var engaged = await LoadEngagedKeysAsync(context, cancellationToken);
        var candidates = filtered.Allowed;

        // strategyKey → per-candidate scores, indexed the same way as `candidates`.
        var scoresByStrategy = new Dictionary<string, IReadOnlyList<RankingScore>>(StringComparer.Ordinal);
        var configurationsByKey = new Dictionary<string, RankingStrategy>(StringComparer.Ordinal);

        foreach (var configuration in applicable)
        {
            var implementation = _strategies.Find(configuration.Key);

            if (implementation is null)
            {
                RecommendationTelemetry.UnimplementedStrategies.Add(
                    1, new KeyValuePair<string, object?>("strategy", configuration.Key));

                _logger.LogWarning(
                    "Ranking strategy '{Key}' is configured on the {Surface} surface but nothing "
                    + "implements it. It contributed nothing.",
                    configuration.Key,
                    context.Surface);

                continue;
            }

            if (context.IsColdStart && !implementation.SupportsColdStart)
            {
                continue;
            }

            try
            {
                var scores = await implementation.RankAsync(
                    new RankingContext
                    {
                        Personalization = context,
                        Candidates = candidates,
                        Configuration = configuration,
                        EngagedTargetKeys = engaged
                    },
                    cancellationToken);

                if (scores.Count != candidates.Count)
                {
                    // The contract is one score per candidate in order. A strategy that breaks it
                    // would silently misattribute every score to the wrong candidate, which is worse
                    // than not running at all.
                    _logger.LogError(
                        "Ranking strategy '{Key}' returned {Returned} scores for {Expected} "
                        + "candidates and was discarded.",
                        configuration.Key,
                        scores.Count,
                        candidates.Count);

                    continue;
                }

                scoresByStrategy[configuration.Key] = scores;
                configurationsByKey[configuration.Key] = configuration;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Ranking strategy '{Key}' failed on the {Surface} surface and was skipped.",
                    configuration.Key,
                    context.Surface);
            }
        }

        return Assemble(candidates, scoresByStrategy, configurationsByKey, filtered, context);
    }

    /// <summary>Folds every strategy's answers into one scored, explained page.</summary>
    private List<ScoredRecommendation> Assemble(
        IReadOnlyList<RecommendationCandidate> candidates,
        Dictionary<string, IReadOnlyList<RankingScore>> scoresByStrategy,
        Dictionary<string, RankingStrategy> configurationsByKey,
        PolicyEvaluationResult filtered,
        PersonalizationContext context)
    {
        var assembled = new List<ScoredRecommendation>(candidates.Count);

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var contributions = new List<StrategyContribution>();
            var perStrategy = new Dictionary<string, RankingScore>(StringComparer.Ordinal);
            var total = 0m;
            var totalWeight = 0m;

            foreach (var (key, scores) in scoresByStrategy)
            {
                var configuration = configurationsByKey[key];
                var implementation = _strategies.Find(key);

                if (implementation is not null && !implementation.AppliesTo(candidate))
                {
                    continue;
                }

                var score = scores[index];

                totalWeight += configuration.Weight;

                if (!score.IsMeaningful || score.Value < configuration.MinimumScore)
                {
                    continue;
                }

                var contribution = StrategyContribution.Create(
                    _guids.NewSequentialGuid(),
                    key,
                    configuration.Kind,
                    score.Value,
                    configuration.Weight);

                contributions.Add(contribution);
                perStrategy[key] = score;
                total += contribution.WeightedScore;
            }

            // Normalised by the weight that was actually available, so a surface with four
            // strategies and one with ten produce comparable scores. Without it a surface's scores
            // would shift every time somebody added a strategy to it.
            var normalized = totalWeight <= 0m ? 0m : Math.Clamp(total / totalWeight, 0m, 1m);

            var demotion = filtered.DemotionFactors.GetValueOrDefault(candidate.Target.Key, 1m);
            var finalScore = decimal.Round(normalized * demotion, 6);

            var primary = contributions.Count == 0
                ? "fallback"
                : contributions.MaxBy(contribution => contribution.WeightedScore)!.StrategyKey;

            assembled.Add(new ScoredRecommendation
            {
                Candidate = candidate,
                Score = finalScore,
                PrimaryStrategyKey = primary,
                Explanation = _reasons.Build(candidate, contributions, perStrategy, context),
                Contributions = contributions
                    .OrderByDescending(contribution => contribution.WeightedScore)
                    .ToList(),
                DemotionFactor = demotion
            });
        }

        return assembled
            .OrderByDescending(item => item.Score)

            // A stable tie-break. Without it, two candidates at the same score come back in whatever
            // order the pool happened to hold them, so two identical requests produce two different
            // pages — which reads, correctly, as a system that is guessing.
            .ThenByDescending(item => item.Candidate.PopularityScore)
            .ThenBy(item => item.Candidate.Target.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The surface's strategy rows, cached. Read every generation, written twice a year.</summary>
    private async Task<IReadOnlyList<RankingStrategy>> LoadStrategiesAsync(
        RecommendationSurface surface, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync(
            StrategyCacheKeyPrefix + surface,
            _options.Value.CacheLifetime,
            async token => (await _strategyCatalog.ListForSurfaceAsync(surface, token)).ToList(),
            cancellationToken);

    /// <summary>
    /// What the member has engaged with, as keys.
    /// <para>
    /// Read once per generation and shared by the similarity and diversity strategies, for the reason
    /// <c>RankingContext</c> records: two reads of one thing in a pass that runs on every app launch
    /// is one read too many.
    /// </para>
    /// </summary>
    private async Task<IReadOnlySet<string>> LoadEngagedKeysAsync(
        PersonalizationContext context, CancellationToken cancellationToken)
    {
        if (context.IsColdStart || !context.IsSurfacePersonalized)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var recent = await _signals.ListForUserAsync(
            context.UserId, _options.Value.ProfileBuildSignalWindow / 10, cancellationToken);

        return recent
            .Where(signal => signal.TargetType is not null && signal.TargetId is not null)
            .Select(signal => $"{signal.TargetType}:{signal.TargetId!.Value:N}")
            .ToHashSet(StringComparer.Ordinal);
    }

    // ── Persistence and telemetry ───────────────────────────────────────────────────────────

    private async Task<RecommendationSetId> PersistAsync(
        RecommendationRequest request,
        PersonalizationContext context,
        IReadOnlyList<ScoredRecommendation> page,
        int candidateCount,
        PolicyEvaluationResult filtered,
        int generationMilliseconds,
        CancellationToken cancellationToken)
    {
        var setId = RecommendationSetId.From(_guids.NewSequentialGuid());

        var items = page.Select((item, position) => RecommendationItem.Create(
            _guids.NewSequentialGuid(),
            item.Candidate.Target,
            position,
            item.Score,
            item.PrimaryStrategyKey,
            item.Explanation,
            item.Contributions)).ToList();

        var set = RecommendationSet.Generate(
            setId,
            request.UserId,
            request.Surface,
            items,
            context.AsOf,
            context.AsOf.Add(_options.Value.SetLifetime),
            context.IsColdStart,
            candidateCount,
            filtered.ExcludedCount,
            generationMilliseconds,
            request.TargetType);

        _sets.Add(set);

        // A write on a read path, and a deliberate one. Generating a page is a query as far as the
        // caller is concerned, but the record of what was served is what makes "why am I seeing
        // this" answerable ten minutes later and what every acceptance rate is measured against. The
        // save also flushes the outbox row the generation event produced.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return setId;
    }

    private static void RecordTelemetry(
        PersonalizationContext context, int itemCount, double milliseconds)
    {
        var surfaceTag = new KeyValuePair<string, object?>("surface", context.Surface.ToString());

        RecommendationTelemetry.SetsGenerated.Add(1, surfaceTag);
        RecommendationTelemetry.ItemsGenerated.Add(itemCount, surfaceTag);
        RecommendationTelemetry.GenerationDuration.Record(milliseconds, surfaceTag);

        if (context.IsColdStart)
        {
            RecommendationTelemetry.ColdStartGenerations.Add(1, surfaceTag);
        }

        if (itemCount == 0)
        {
            RecommendationTelemetry.EmptyGenerations.Add(1, surfaceTag);
        }
    }
}
