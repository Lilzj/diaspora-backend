using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>Cancels a placement the caller bought.</summary>
public sealed class CancelPlacementCommandHandler
    : ICommandHandler<CancelPlacementCommand, PlacementResponse>
{
    private readonly IPlacementRepository _placements;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPlacementCommandHandler(
        IPlacementRepository placements,
        FinancialAuthorizationService authorization,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _placements = placements;
        _authorization = authorization;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PlacementResponse>> Handle(
        CancelPlacementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PlacementResponse>(caller.Error);
        }

        var placement = await _placements.FindAsync(
            PlacementId.From(command.PlacementId), cancellationToken);

        if (placement is null
            || (placement.PurchasedByUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.CatalogManage)))
        {
            return Result.Failure<PlacementResponse>(Error.NotFound(
                FinancialErrorCodes.PlacementNotFound, "That placement does not exist."));
        }

        var now = _clock.UtcNow;

        try
        {
            placement.Cancel(command.Reason ?? "Cancelled by the purchaser.", now);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PlacementResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _placements.Update(placement);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(placement, now));
    }
}
