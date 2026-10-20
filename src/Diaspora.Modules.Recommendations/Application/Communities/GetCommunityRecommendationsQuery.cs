using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Application.Communities;

/// <summary>
/// Communities to suggest — <c>GET /recommendations/communities</c>.
/// <para>
/// <b>Why this surface left the engine.</b> The engine ranks projected candidates, and no community
/// was ever projected: nothing consumed a community-created event, so the surface answered an empty
/// page to everybody, and its <c>country</c> parameter could not influence a pool that did not
/// exist. A candidate row also holds one country, where a community is present in several. So the
/// communities come from CommunityProgramme through <see cref="ICommunitySuggestionSource"/>,
/// filtered by the presence country; <see cref="CommunityRanker"/> orders them; and the page is
/// recorded as an ordinary set, so <c>POST /recommendations/dismiss</c> with
/// <c>targetType=Community</c> and <c>surface=Communities</c> keeps a dismissed community away.
/// </para>
/// <para>
/// <b>The country.</b> <c>country</c> when sent; otherwise the member's stated country, then the one
/// their profile records. An explicit country is strict — a community not present there is not
/// suggested. An inferred one widens to everywhere when it finds nothing, the same ladder the cold
/// start uses, because an empty rail tells a new member nothing.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.FeedRead)]
public sealed record GetCommunityRecommendationsQuery : IQuery<RecommendationPageResponse>
{
    /// <summary>The presence country — where the communities should be active. ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Community types (<c>NationalDiaspora</c>, <c>FaithCommunity</c>…). Empty accepts every type.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    public int? Limit { get; init; }
}

public sealed class GetCommunityRecommendationsQueryValidator
    : AbstractValidator<GetCommunityRecommendationsQuery>
{
    public GetCommunityRecommendationsQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .Must(CountryCodes.IsWellFormed)
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode))
            .WithMessage("A country is an ISO 3166-1 alpha-2 code, such as GB.");

        RuleFor(query => query.Limit)
            .GreaterThanOrEqualTo(1)
            .When(query => query.Limit is not null);
    }
}

public sealed class GetCommunityRecommendationsQueryHandler
    : IQueryHandler<GetCommunityRecommendationsQuery, RecommendationPageResponse>
{
    /// <summary>How many joinable communities are read before ranking. The source caps it at its page size.</summary>
    private const int PoolSize = 50;

    private readonly RecommendationAuthorizationService _authorization;
    private readonly IEnumerable<ICommunitySuggestionSource> _sources;
    private readonly IRecommendationFeedbackRepository _feedback;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly IPersonalizationProfileRepository _profiles;
    private readonly ServedSetRecorder _recorder;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly IClock _clock;

    public GetCommunityRecommendationsQueryHandler(
        RecommendationAuthorizationService authorization,
        IEnumerable<ICommunitySuggestionSource> sources,
        IRecommendationFeedbackRepository feedback,
        IPersonalizationPreferenceRepository preferences,
        IPersonalizationProfileRepository profiles,
        ServedSetRecorder recorder,
        IOptions<RecommendationOptions> options,
        IClock clock)
    {
        _authorization = authorization;
        _sources = sources;
        _feedback = feedback;
        _preferences = preferences;
        _profiles = profiles;
        _recorder = recorder;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<RecommendationPageResponse>> Handle(
        GetCommunityRecommendationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<RecommendationPageResponse>(caller.Error);
        }

        var userId = caller.Value;
        var now = _clock.UtcNow;
        var options = _options.Value;
        var limit = Math.Clamp(query.Limit ?? options.DefaultPageSize, 1, options.MaximumPageSize);

        var source = _sources.FirstOrDefault();

        if (source is null)
        {
            return Result.Success(Page([], null, 0, 0, now));
        }

        var preference = await _preferences.FindAsync(userId, cancellationToken);
        var profile = await _profiles.FindAsync(userId, cancellationToken);
        var personalised = preference?.IsPersonalizationEnabled != false;

        var requested = CountryCodes.Normalise(query.CountryCode);
        var country = requested
            ?? CountryCodes.Normalise(preference?.PreferredCountryCode)
            ?? CountryCodes.Normalise(profile?.PrimaryCountryCode);

        var candidates = await source.ListAsync(
            new CommunitySuggestionQuery
            {
                ViewerUserId = userId.Value,
                PresenceCountryCode = country,
                Take = PoolSize
            },
            cancellationToken);

        // An inferred country with nothing in it widens; an asked-for one does not. See the class summary.
        if (candidates.Count == 0 && requested is null && country is not null)
        {
            candidates = await source.ListAsync(
                new CommunitySuggestionQuery { ViewerUserId = userId.Value, Take = PoolSize },
                cancellationToken);
        }

        if (query.Categories.Count > 0)
        {
            var types = query.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            candidates = [.. candidates.Where(candidate => types.Contains(candidate.Type))];
        }

        var suppressed = (await _feedback.ListSuppressedTargetIdsAsync(
                userId, RecommendationTargetType.Community, now, cancellationToken))
            .Concat(preference?.BlockedTargets
                        .Where(block => block.TargetType == RecommendationTargetType.Community)
                        .Select(block => block.TargetId)
                    ?? [])
            .ToHashSet();

        var ranked = CommunityRanker.Rank(new CommunityRankingInput
        {
            Candidates = candidates,
            CountryCode = country,
            CountryInterest = personalised && profile is not null
                ? code => profile.LocationAffinityFor(AffinityScope.Country, code)
                : null,
            LanguageCode = personalised
                ? preference?.PreferredLanguage ?? profile?.PreferredLanguage
                : null,
            Suppressed = suppressed,
            Limit = limit
        });

        var excluded = candidates.Count(candidate => suppressed.Contains(candidate.CommunityId));

        var setId = await _recorder.RecordAsync(
            userId,
            RecommendationSurface.Communities,
            RecommendationTargetType.Community,
            CommunityRanker.StrategyKey,
            [.. ranked.Select(item => new ServedItem(
                item.Community.CommunityId, item.Score, item.ReasonCode, item.ReasonText))],
            candidates.Count,
            excluded,
            now,
            cancellationToken);

        return Result.Success(Page(ranked, setId?.Value, candidates.Count, excluded, now, country));
    }

    private static RecommendationPageResponse Page(
        IReadOnlyList<RankedCommunity> ranked,
        Guid? setId,
        int candidateCount,
        int excludedCount,
        DateTimeOffset generatedAt,
        string? country = null) =>
        new()
        {
            Items =
            [
                .. ranked.Select((item, position) => new RecommendationResponse
                {
                    TargetType = nameof(RecommendationTargetType.Community),
                    TargetId = item.Community.CommunityId,
                    Title = item.Community.Name,
                    Summary = item.Community.Tagline,
                    Slug = item.Community.Slug,
                    MediaReference = item.Community.LogoMediaId?.ToString(),
                    CategoryKey = item.Community.Type,
                    Location = new RecommendationLocationResponse
                    {
                        // Where it was matched, when it was; where its people are from otherwise.
                        CountryCode = item.IsPresentInCountry ? country : item.Community.OriginCountryCode,
                        RegionName = item.IsPresentInCountry ? null : item.Community.OriginRegionName
                    },
                    IsVerified = false,
                    IsFeatured = false,
                    PublishedAt = item.Community.CreatedAt,
                    Position = position,
                    Score = item.Score,
                    Strategy = CommunityRanker.StrategyKey,
                    Reason = RecommendationMapper.ToResponse(
                        ServedSetRecorder.Explain(new ServedItem(
                            item.Community.CommunityId, item.Score, item.ReasonCode, item.ReasonText))),
                    Contributions = []
                })
            ],
            SetId = setId,
            Surface = nameof(RecommendationSurface.Communities),
            IsColdStart = false,
            IsCached = false,
            CandidateCount = candidateCount,
            ExcludedCount = excludedCount,
            Strategies = ranked.Count == 0 ? [] : [CommunityRanker.StrategyKey],
            GeneratedAt = generatedAt
        };
}
