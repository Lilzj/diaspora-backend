using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>
/// Creates or revises an accommodation type.
/// <para>
/// <b>The key decides which it is, and the key is never editable.</b> A key that already exists is
/// an update; one that does not is a create. Renaming a type therefore changes what members read
/// and leaves every listing that claimed it intact — which is the whole reason listings store the
/// key rather than the identifier.
/// </para>
/// <para>
/// <b>Retiring is not deleting, and neither is offered here.</b> <c>IsActive = false</c> stops a
/// type being selectable and leaves existing listings alone. Deleting one would orphan every
/// listing that used it, and a housing platform that silently reclassified somebody's flat as
/// "unknown" would be worse than one carrying a retired type for ever.
/// </para>
/// </summary>
public sealed class UpsertAccommodationTypeCommandHandler
    : ICommandHandler<UpsertAccommodationTypeCommand, AccommodationTypeResponse>
{
    private readonly IAccommodationTypeRepository _types;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpsertAccommodationTypeCommandHandler(
        IAccommodationTypeRepository types, IUnitOfWork unitOfWork, IClock clock)
    {
        _types = types;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AccommodationTypeResponse>> Handle(
        UpsertAccommodationTypeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Result.Failure<AccommodationTypeResponse>(
                HousingErrors.AccommodationTypeInvalid);
        }

        var existing = await _types.GetByKeyAsync(request.Key, cancellationToken);

        if (existing is null)
        {
            var created = AccommodationType.Create(
                AccommodationTypeId.New(),
                request.Key,
                request.Name,
                _clock.UtcNow,
                request.Description,
                request.IconKey,
                request.IsShared,
                request.IsTemporary,
                request.Order);

            if (created.IsFailure)
            {
                return Result.Failure<AccommodationTypeResponse>(created.Error);
            }

            if (request.IsActive == false)
            {
                created.Value.Retire();
            }

            await _types.AddAsync(created.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(HousingCatalogMapper.ToResponse(created.Value));
        }

        var updated = existing.Update(
            request.Name,
            request.Description,
            request.IconKey,
            request.IsShared,
            request.IsTemporary,
            request.Order);

        if (updated.IsFailure)
        {
            return Result.Failure<AccommodationTypeResponse>(updated.Error);
        }

        if (request.IsActive is { } active)
        {
            if (active)
            {
                existing.Reinstate();
            }
            else
            {
                existing.Retire();
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(HousingCatalogMapper.ToResponse(existing));
    }
}
