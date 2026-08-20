using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>
/// Revises the caller's landlord profile.
/// <para>
/// Every field is optional and null means leave it alone, so the aggregate is asked to change only
/// what actually moved and the update event names exactly those fields.
/// </para>
/// </summary>
public sealed class UpdateLandlordCommandHandler
    : ICommandHandler<UpdateLandlordCommand, LandlordProfileResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingNeighborhoodResolver _geography;
    private readonly HousingProviderResolver _providers;

    public UpdateLandlordCommandHandler(
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingNeighborhoodResolver geography,
        HousingProviderResolver providers)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _geography = geography;
        _providers = providers;
    }

    public async Task<Result<LandlordProfileResponse>> Handle(
        UpdateLandlordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The caller's own profile whatever state it is in: a suspended landlord must still be
        // able to correct their contact details and close the profile.
        var profile = await _authorization.FindCallerLandlordAsync(cancellationToken);

        if (profile is null)
        {
            return Result.Failure<LandlordProfileResponse>(HousingErrors.NotLandlord);
        }

        var request = command.Request;

        var identity = BuildIdentity(profile, request);

        if (identity.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(identity.Error);
        }

        var contact = LandlordContact.Create(
            request.PhoneNumber ?? profile.Contact.PhoneNumber,
            request.Email ?? profile.Contact.Email,
            request.WebsiteUrl ?? profile.Contact.WebsiteUrl,
            request.ShowContactPublicly ?? profile.Contact.ShowPublicly);

        if (contact.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(contact.Error);
        }

        if (request.CountryCode is not null
            && !await _geography.CountryExistsAsync(request.CountryCode, cancellationToken))
        {
            return Result.Failure<LandlordProfileResponse>(HousingErrors.CountryInvalid);
        }

        var updated = profile.Update(
            request.BaseRevision,
            identity.Value,
            contact.Value,
            request.Bio,
            request.CountryCode);

        if (updated.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(updated.Error);
        }

        if (request.LogoMediaId is { } logoId)
        {
            profile.SetLogo(logoId == Guid.Empty ? null : logoId);
        }

        if (request.ProviderId is { } providerId)
        {
            var caller = _authorization.RequireCaller();

            if (caller.IsFailure)
            {
                return Result.Failure<LandlordProfileResponse>(caller.Error);
            }

            var linked = await _providers.LinkAsync(
                profile, providerId, caller.Value, cancellationToken);

            if (linked.IsFailure)
            {
                return Result.Failure<LandlordProfileResponse>(linked.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(HousingLandlordMapper.ToResponse(
            profile, isOwner: true, isStaff: false, hasConfirmedViewing: true));
    }

    /// <summary>
    /// Rebuilds the identity from whatever the caller supplied, falling back to what is stored.
    /// <para>
    /// The kind is deliberately not editable. A member who registered as an individual and later
    /// becomes an agency is making a claim Trust has to re-examine, and silently flipping the kind
    /// would carry an individual's verification onto a business.
    /// </para>
    /// </summary>
    private static Result<LandlordIdentity> BuildIdentity(
        Domain.Landlords.LandlordProfile profile,
        Contracts.V1.Requests.UpdateLandlordRequest request)
    {
        if (request.DisplayName is null
            && request.LegalName is null
            && request.RegistrationNumber is null)
        {
            return Result.Success(profile.Identity);
        }

        return LandlordIdentity.Create(
            request.DisplayName ?? profile.Identity.DisplayName,
            profile.Identity.Kind,
            request.LegalName ?? profile.Identity.LegalName,
            request.RegistrationNumber ?? profile.Identity.RegistrationNumber);
    }
}
