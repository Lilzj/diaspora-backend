using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Countries;

/// <summary>Serves the country catalogue.</summary>
public sealed class ListCountriesQueryHandler
    : IQueryHandler<ListCountriesQuery, PagedResult<CountrySummaryResponse>>
{
    private readonly ICountryRepository _countries;

    public ListCountriesQueryHandler(ICountryRepository countries)
    {
        _countries = countries;
    }

    public async Task<Result<PagedResult<CountrySummaryResponse>>> Handle(
        ListCountriesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await _countries.SearchAsync(
            request.Term,
            request.SupportedOnly,
            request.Continent,
            request.Pagination.Sanitize(),
            cancellationToken);

        return Result.Success(page.Map(LocationMapper.ToSummary));
    }
}
