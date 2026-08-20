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
/// Creates or revises an amenity.
/// <para>
/// The key decides create from update and is never editable, for the same reason as the
/// accommodation type: listings store the key, so renaming an amenity changes what members read and
/// leaves every listing that claimed it intact.
/// </para>
/// <para>
/// <b><c>IsAccessibilityFeature</c> is not a presentation flag.</b> It is what lets a client render
/// step-free access as a filter of its own rather than as item forty in a checkbox list, and for
/// the members who need it the difference is whether a property is habitable at all. An operator
/// adding an accessibility amenity must set it, and this is the endpoint where they do.
/// </para>
/// </summary>
public sealed class UpsertHousingAmenityCommandHandler
    : ICommandHandler<UpsertHousingAmenityCommand, HousingAmenityCatalogResponse>
{
    private readonly IHousingAmenityRepository _amenities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpsertHousingAmenityCommandHandler(
        IHousingAmenityRepository amenities, IUnitOfWork unitOfWork, IClock clock)
    {
        _amenities = amenities;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<HousingAmenityCatalogResponse>> Handle(
        UpsertHousingAmenityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Result.Failure<HousingAmenityCatalogResponse>(HousingErrors.AmenityInvalid);
        }

        var existing = await _amenities.GetByKeyAsync(request.Key, cancellationToken);

        if (existing is null)
        {
            var created = HousingAmenity.Create(
                HousingAmenityId.New(),
                request.Key,
                request.Name,
                _clock.UtcNow,
                request.Group,
                request.IconKey,
                request.IsAccessibilityFeature,
                request.IsFilterable,
                request.Order);

            if (created.IsFailure)
            {
                return Result.Failure<HousingAmenityCatalogResponse>(created.Error);
            }

            if (request.IsActive == false)
            {
                created.Value.Retire();
            }

            await _amenities.AddAsync(created.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(HousingCatalogMapper.ToResponse(created.Value));
        }

        var updated = existing.Update(
            request.Name,
            request.Group,
            request.IconKey,
            request.IsAccessibilityFeature,
            request.IsFilterable,
            request.Order);

        if (updated.IsFailure)
        {
            return Result.Failure<HousingAmenityCatalogResponse>(updated.Error);
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
