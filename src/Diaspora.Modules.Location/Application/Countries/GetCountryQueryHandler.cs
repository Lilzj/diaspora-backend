using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Countries;

/// <summary>Serves one country, optionally with contributed counts.</summary>
public sealed class GetCountryQueryHandler : IQueryHandler<GetCountryQuery, CountryDetailResponse>
{
    private readonly ICountryRepository _countries;
    private readonly ResourceCountAggregator _counts;

    public GetCountryQueryHandler(ICountryRepository countries, ResourceCountAggregator counts)
    {
        _countries = countries;
        _counts = counts;
    }

    public async Task<Result<CountryDetailResponse>> Handle(
        GetCountryQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = CountryCode.TryCreate(request.CountryCode);

        if (code.IsFailure)
        {
            return Result.Failure<CountryDetailResponse>(code.Error);
        }

        var country = await _countries.GetByCodeAsync(code.Value, cancellationToken);

        if (country is null)
        {
            return Result.Failure<CountryDetailResponse>(
                LocationErrors.CountryNotFound(code.Value.Value));
        }

        var counts = request.IncludeCounts
            ? await _counts.CountInCountryAsync(code.Value, cancellationToken)
            : new Dictionary<string, int>(StringComparer.Ordinal);

        return Result.Success(LocationMapper.ToDetail(country, counts));
    }
}
