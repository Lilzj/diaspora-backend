using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>
/// Applies a seller's edit to their own profile.
/// <para>
/// <b>A suspended seller may still edit</b>, which is why this reads the profile directly rather
/// than through the trading guard: somebody who has been barred needs to be able to correct their
/// details and, above all, to close the profile.
/// </para>
/// </summary>
public sealed class UpdateSellerCommandHandler
    : ICommandHandler<UpdateSellerCommand, SellerProfileResponse>
{
    private readonly ISellerProfileRepository _sellers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public UpdateSellerCommandHandler(
        ISellerProfileRepository sellers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _sellers = sellers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<SellerProfileResponse>> Handle(
        UpdateSellerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await _authorization.FindCallerSellerAsync(cancellationToken);

        if (profile is null)
        {
            return Result.Failure<SellerProfileResponse>(MarketplaceErrors.NotSeller);
        }

        var request = command.Request;

        var identity = SellerIdentity.Create(
            request.DisplayName,
            request.LegalName,
            request.RegistrationNumber,
            MarketplaceWireNames.ParseSellerKind(request.Kind) ?? profile.Identity.Kind,
            request.Bio);

        if (identity.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(identity.Error);
        }

        var contact = SellerContact.Create(
            request.Phone,
            request.Email,
            request.Website,
            MarketplaceWireNames.ParseContactPreference(request.ContactPreference)
                ?? profile.Contact.Preference);

        if (contact.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(contact.Error);
        }

        var updated = profile.UpdateProfile(
            identity.Value,
            contact.Value,
            request.CountryCode,
            _clock.UtcNow,
            request.BaseRevision,
            _authorization.CallerId);

        if (updated.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(updated.Error);
        }

        if (request.LogoMediaId is not null || request.BannerMediaId is not null)
        {
            var branding = profile.SetBranding(
                request.LogoMediaId, request.BannerMediaId, baseRevision: null);

            if (branding.IsFailure)
            {
                return Result.Failure<SellerProfileResponse>(branding.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SellerMapper.ToResponse(
            profile, canManage: true, _authorization.IsStaff));
    }
}
