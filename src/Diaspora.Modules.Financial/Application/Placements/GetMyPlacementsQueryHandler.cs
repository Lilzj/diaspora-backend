using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>Reads the caller's placements.</summary>
public sealed class GetMyPlacementsQueryHandler
    : IQueryHandler<GetMyPlacementsQuery, IReadOnlyList<PlacementResponse>>
{
    private readonly IPlacementRepository _placements;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetMyPlacementsQueryHandler(
        IPlacementRepository placements,
        FinancialAuthorizationService authorization,
        IClock clock)
    {
        _placements = placements;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<PlacementResponse>>> Handle(
        GetMyPlacementsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PlacementResponse>>(caller.Error);
        }

        var status = Enum.TryParse<PlacementStatus>(query.Status, true, out var wanted)
            ? wanted
            : (PlacementStatus?)null;

        var now = _clock.UtcNow;

        var placements = await _placements.ListByPurchaserAsync(
            caller.Value, status, query.Before, query.ExcludeIds, query.Take, cancellationToken);

        return Result.Success<IReadOnlyList<PlacementResponse>>(
            [.. placements.Select(placement => FinancialMapper.ToResponse(placement, now))]);
    }
}
