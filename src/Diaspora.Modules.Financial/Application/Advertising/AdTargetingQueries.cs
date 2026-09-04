using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Extensibility.Advertising;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>
/// What a targeting dimension offers — MON-GAP-007.
/// <para>
/// The register asks for the selectable values to be "owned by the backend rather than hard-coded
/// in the app", because a client with its own copy disagrees with the delivery engine the first
/// time one of them changes.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record GetAdTargetingOptionsQuery : IQuery<AdTargetingOptionsResponse>
{
    /// <summary>An <c>AdTargetingDimension</c> name.</summary>
    public string? Dimension { get; init; }

    /// <summary>Narrows the list. A picker over every community needs a search, not a page.</summary>
    public string? Query { get; init; }

    public int? Take { get; init; }
}

/// <summary>How many people a selection could reach — MON-GAP-007.</summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record EstimateAdAudienceQuery : IQuery<AdAudienceEstimateResponse>
{
    public AdAudienceRequest? Audience { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class GetAdTargetingOptionsQueryHandler
    : IQueryHandler<GetAdTargetingOptionsQuery, AdTargetingOptionsResponse>
{
    private readonly IEnumerable<IAdTargetingOptionProvider> _providers;

    public GetAdTargetingOptionsQueryHandler(IEnumerable<IAdTargetingOptionProvider> providers) =>
        _providers = providers;

    public async Task<Result<AdTargetingOptionsResponse>> Handle(
        GetAdTargetingOptionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Enum.TryParse<AdTargetingDimension>(
                query.Dimension?.Trim(), ignoreCase: true, out var dimension)
            || !Enum.IsDefined(dimension))
        {
            return Result.Failure<AdTargetingOptionsResponse>(AdCampaignErrors.DimensionUnknown);
        }

        var provider = _providers.FirstOrDefault(candidate => candidate.Dimension == dimension);

        // No catalogue for this dimension, which is a different answer from an empty catalogue.
        // Country is the deliberate case: it is an ISO standard the device already holds in full,
        // and a second copy served from here would be a second list to keep in step for nothing.
        if (provider is null)
        {
            return Result.Success(new AdTargetingOptionsResponse
            {
                Dimension = dimension.ToString(),
                Options = [],
                HasCatalogue = false,
                Note = dimension == AdTargetingDimension.Country
                    ? "Countries are an ISO list the app already holds. Pick from that."
                    : "Diaspora cannot list these. Type the values you want to target."
            });
        }

        var options = await provider.GetOptionsAsync(
            query.Query, Math.Clamp(query.Take ?? 50, 1, 200), cancellationToken);

        return Result.Success(new AdTargetingOptionsResponse
        {
            Dimension = dimension.ToString(),
            Options = [.. options.Select(option => new AdTargetingOptionResponse
            {
                Value = option.Value,
                Label = option.Label
            })],
            HasCatalogue = true,
            Note = null
        });
    }
}

/// <summary>
/// Estimates the reachable audience.
/// <para>
/// <b>A range and a basis, never a number.</b> §29 forbids inventing an authoritative metric, and a
/// single figure would be exactly that — the reachable audience depends on who opens the app
/// tomorrow. The bounds come from whichever dimensions can genuinely be counted; the rest
/// contribute nothing and the response says which were used.
/// </para>
/// <para>
/// <b>The dimensions intersect, so the narrowest bound wins.</b> A selection of two communities and
/// three interests reaches at most the people in those communities <em>and</em> holding those
/// interests — so the upper bound is the smallest upper any sizer reported, not their sum. Getting
/// that backwards would quote an advertiser a reach larger than any one of its own constraints
/// allows.
/// </para>
/// </summary>
public sealed class EstimateAdAudienceQueryHandler
    : IQueryHandler<EstimateAdAudienceQuery, AdAudienceEstimateResponse>
{
    private readonly IEnumerable<IAdAudienceSizer> _sizers;

    public EstimateAdAudienceQueryHandler(IEnumerable<IAdAudienceSizer> sizers) => _sizers = sizers;

    public async Task<Result<AdAudienceEstimateResponse>> Handle(
        EstimateAdAudienceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var audience = AdCampaignFactory.ReadAudience(query.Audience);
        var constrained = audience.ConstrainedDimensions();

        if (audience.IsEveryone)
        {
            return Result.Success(new AdAudienceEstimateResponse
            {
                LowerBound = 0,
                UpperBound = 0,
                ConstrainedDimensions = [],
                IsNarrowed = false,
                IsCounted = false,
                Basis =
                    "This campaign is not narrowed to anybody in particular, so there is no size to "
                    + "estimate. Diaspora does not publish how many members it has."
            });
        }

        var bounds = new List<AdAudienceBounds>();

        foreach (var sizer in _sizers)
        {
            if (!constrained.Contains(sizer.Dimension))
            {
                continue;
            }

            if (await sizer.EstimateAsync(audience, cancellationToken) is { } estimate)
            {
                bounds.Add(estimate);
            }
        }

        if (bounds.Count == 0)
        {
            return Result.Success(new AdAudienceEstimateResponse
            {
                LowerBound = 0,
                UpperBound = 0,

                // The dimensions are still reported: the campaign *is* narrowed, the platform just
                // cannot say by how much. Reporting none would read as an unnarrowed campaign.
                ConstrainedDimensions = [.. constrained.Select(dimension => dimension.ToString())],
                IsNarrowed = true,
                IsCounted = false,
                Basis =
                    "This campaign is narrowed, but Diaspora cannot count how many people that "
                    + "reaches. It will only be shown to people who match."
            });
        }

        // Intersecting constraints: the tightest upper bound is the real ceiling, and the tightest
        // lower bound is the most anybody can promise. Summing would quote a reach larger than any
        // single constraint allows.
        var upper = bounds.Min(bound => bound.Upper);
        var lower = Math.Min(bounds.Min(bound => bound.Lower), upper);

        return Result.Success(new AdAudienceEstimateResponse
        {
            LowerBound = lower,
            UpperBound = upper,
            ConstrainedDimensions = [.. constrained.Select(dimension => dimension.ToString())],
            IsNarrowed = true,
            IsCounted = true,
            Basis = string.Join(' ', bounds.Select(bound => bound.Basis))
        });
    }
}
