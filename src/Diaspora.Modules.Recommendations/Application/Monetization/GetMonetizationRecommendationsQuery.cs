using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Monetization;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Infrastructure.Explainability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Recommendations.Application.Monetization;

/// <summary>
/// MON-GAP-011: plans and promotions the caller could buy — <c>GET /recommendations/monetization</c>.
/// <para>
/// <b>Not the engine, on purpose.</b> The engine ranks a pool of other people's content; these
/// candidates are the caller's own items crossed with a catalogue Financial owns, and the reasons are
/// facts about the caller's account. So the facts come through <see cref="IMonetizationOfferSource"/>,
/// the ranking is <see cref="MonetizationRanker"/>, and what was served is recorded as an ordinary
/// <c>RecommendationSet</c> so the ordinary feedback routes work on it.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.FeedRead)]
public sealed record GetMonetizationRecommendationsQuery
    : IQuery<IReadOnlyList<MonetizationRecommendationResponse>>
{
    public int? Limit { get; init; }

    /// <summary>Which country's plans and rules apply. Defaults to the member's own.</summary>
    public string? CountryCode { get; init; }
}

public sealed class GetMonetizationRecommendationsQueryValidator
    : AbstractValidator<GetMonetizationRecommendationsQuery>
{
    public GetMonetizationRecommendationsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, MonetizationRecommendationLimits.MaxLimit)
            .When(query => query.Limit is not null);

        RuleFor(query => query.CountryCode)
            .Must(CountryCodes.IsWellFormed)
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode))
            .WithMessage("A country is an ISO 3166-1 alpha-2 code, such as GB.");
    }
}

public static class MonetizationRecommendationLimits
{
    public const int DefaultLimit = 10;

    public const int MaxLimit = 50;

    /// <summary>How many of the member's own live items are considered.</summary>
    public const int OwnedItemWindow = 100;
}

public sealed class GetMonetizationRecommendationsQueryHandler
    : IQueryHandler<GetMonetizationRecommendationsQuery, IReadOnlyList<MonetizationRecommendationResponse>>
{
    /// <summary>The owned item types a promotion can be bought for — Financial's subject type names.</summary>
    private static readonly RecommendationTargetType[] PromotableTypes =
    [
        RecommendationTargetType.MarketplaceListing,
        RecommendationTargetType.HousingListing,
        RecommendationTargetType.Job,
        RecommendationTargetType.Provider,
        RecommendationTargetType.Organization,
        RecommendationTargetType.Event,
        RecommendationTargetType.Guide
    ];

    private readonly RecommendationAuthorizationService _authorization;
    private readonly IEnumerable<IMonetizationOfferSource> _offers;
    private readonly IRecommendationCandidateRepository _candidates;
    private readonly IRecommendationFeedbackRepository _feedback;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly IPersonalizationProfileRepository _profiles;
    private readonly ServedSetRecorder _recorder;
    private readonly IClock _clock;

    public GetMonetizationRecommendationsQueryHandler(
        RecommendationAuthorizationService authorization,
        IEnumerable<IMonetizationOfferSource> offers,
        IRecommendationCandidateRepository candidates,
        IRecommendationFeedbackRepository feedback,
        IPersonalizationPreferenceRepository preferences,
        IPersonalizationProfileRepository profiles,
        ServedSetRecorder recorder,
        IClock clock)
    {
        _authorization = authorization;
        _offers = offers;
        _candidates = candidates;
        _feedback = feedback;
        _preferences = preferences;
        _profiles = profiles;
        _recorder = recorder;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<MonetizationRecommendationResponse>>> Handle(
        GetMonetizationRecommendationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MonetizationRecommendationResponse>>(caller.Error);
        }

        // No offer source means Financial is not composed into this host: nothing can be bought,
        // so nothing is suggested. An empty list, not an error the client would have to explain.
        var source = _offers.FirstOrDefault();

        if (source is null)
        {
            return Result.Success<IReadOnlyList<MonetizationRecommendationResponse>>([]);
        }

        var userId = caller.Value;
        var now = _clock.UtcNow;
        var preference = await _preferences.FindAsync(userId, cancellationToken);

        // The master switch. These suggestions are made from the member's own listings and
        // purchases; a member who turned personalization off has asked for none of that.
        if (preference?.IsPersonalizationEnabled == false)
        {
            return Result.Success<IReadOnlyList<MonetizationRecommendationResponse>>([]);
        }

        var country = CountryCodes.Normalise(query.CountryCode)
            ?? preference?.PreferredCountryCode
            ?? (await _profiles.FindAsync(userId, cancellationToken))?.PrimaryCountryCode;

        var owned = await _candidates.ListOwnedLiveAsync(
            userId, PromotableTypes, now, MonetizationRecommendationLimits.OwnedItemWindow, cancellationToken);

        var subjects = owned
            .Select(candidate => new OwnedSubject(
                candidate.Target.TargetType.ToString(),
                candidate.Target.TargetId,
                candidate.Title,
                candidate.PublishedAt))
            .ToList();

        var snapshot = await source.ReadAsync(
            new MonetizationOfferQuery
            {
                UserId = userId.Value,
                CountryCode = country,
                Subjects = [.. subjects.Select(subject => new MonetizationSubject(subject.SubjectType, subject.SubjectId))]
            },
            cancellationToken);

        var suppressed = (await _feedback.ListSuppressedTargetIdsAsync(
                userId, RecommendationTargetType.MonetizationOffer, now, cancellationToken))
            .Concat(preference?.BlockedTargets
                        .Where(block => block.TargetType == RecommendationTargetType.MonetizationOffer)
                        .Select(block => block.TargetId)
                    ?? [])
            .ToHashSet();

        var ranking = MonetizationRanker.Rank(new MonetizationRankingInput
        {
            Snapshot = snapshot,
            OwnedSubjects = subjects,
            Suppressed = suppressed,
            AsOf = now,
            Limit = query.Limit ?? MonetizationRecommendationLimits.DefaultLimit
        });

        var setId = await _recorder.RecordAsync(
            userId,
            RecommendationSurface.Monetization,
            RecommendationTargetType.MonetizationOffer,
            MonetizationRanker.StrategyKey,
            [.. ranking.Items.Select(item => new ServedItem(
                item.OfferId, item.Confidence, item.ReasonKind, item.Reason))],
            ranking.CandidateCount,
            ranking.ExcludedCount,
            now,
            cancellationToken);

        return Result.Success<IReadOnlyList<MonetizationRecommendationResponse>>(
        [
            .. ranking.Items.Select((item, position) => new MonetizationRecommendationResponse
            {
                RecommendationId = item.OfferId.ToString("D"),
                Kind = item.Kind,
                Code = item.Code,
                Title = item.Title,
                Reason = item.Reason,
                ReasonCode = item.ReasonCode,
                ReasonResourceKey = RecommendationReasonCatalog.ResourceKeyFor(item.ReasonKind),
                Confidence = item.Confidence,
                Route = item.Route,
                SubjectType = item.SubjectType,
                SubjectId = item.SubjectId,
                Price = item.PriceAmount is { } amount && item.Currency is { } currency
                    ? new MonetizationPriceResponse { Amount = amount, Currency = currency }
                    : null,
                TargetType = nameof(RecommendationTargetType.MonetizationOffer),
                TargetId = item.OfferId,
                Surface = nameof(RecommendationSurface.Monetization),
                SetId = setId?.Value,
                Position = position
            })
        ]);
    }
}
