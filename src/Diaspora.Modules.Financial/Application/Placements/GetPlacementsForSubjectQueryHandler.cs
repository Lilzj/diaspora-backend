using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>Reads what is in force over a subject.</summary>
public sealed class GetPlacementsForSubjectQueryHandler
    : IQueryHandler<GetPlacementsForSubjectQuery, IReadOnlyList<PlacementResponse>>
{
    private readonly IPlacementRepository _placements;
    private readonly IClock _clock;

    public GetPlacementsForSubjectQueryHandler(IPlacementRepository placements, IClock clock)
    {
        _placements = placements;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<PlacementResponse>>> Handle(
        GetPlacementsForSubjectQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Enum.TryParse<MonetizationSubjectType>(query.SubjectType, true, out var subjectType))
        {
            return Result.Failure<IReadOnlyList<PlacementResponse>>(Error.Validation(
                FinancialErrorCodes.ProductSubjectMismatch,
                $"'{query.SubjectType}' is not a subject type the platform monetises."));
        }

        var now = _clock.UtcNow;

        var placements = await _placements.ListActiveForSubjectAsync(
            SubjectReference.Create(subjectType, query.SubjectId), now, cancellationToken);

        return Result.Success<IReadOnlyList<PlacementResponse>>(
            [.. placements.Select(placement => FinancialMapper.ToResponse(placement, now))]);
    }
}
