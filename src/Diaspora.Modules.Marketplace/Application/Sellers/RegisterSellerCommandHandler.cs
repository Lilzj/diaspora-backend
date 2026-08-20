using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>
/// Creates the caller's seller profile.
/// <para>
/// <b>A storefront link is validated against B7 and against ownership, in that order.</b> A shop
/// that does not exist is a typo; a shop somebody else owns is an attempt to trade on another
/// business's reputation, and it is the one check here worth being strict about. Both questions are
/// B7's, because only B7 knows.
/// </para>
/// </summary>
public sealed class RegisterSellerCommandHandler
    : ICommandHandler<RegisterSellerCommand, SellerProfileResponse>
{
    private readonly ISellerProfileRepository _sellers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingSlugAllocator _slugs;
    private readonly MarketplaceProviderResolver _providers;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RegisterSellerCommandHandler(
        ISellerProfileRepository sellers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingSlugAllocator slugs,
        MarketplaceProviderResolver providers,
        IGuidGenerator guids,
        IClock clock)
    {
        _sellers = sellers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _slugs = slugs;
        _providers = providers;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<SellerProfileResponse>> Handle(
        RegisterSellerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(caller.Error);
        }

        var request = command.Request;

        if (await _sellers.ExistsForUserAsync(caller.Value, cancellationToken))
        {
            return Result.Failure<SellerProfileResponse>(
                MarketplaceErrors.SellerAlreadyExists);
        }

        var identity = SellerIdentity.Create(
            request.DisplayName,
            request.LegalName,
            request.RegistrationNumber,
            MarketplaceWireNames.ParseSellerKind(request.Kind) ?? SellerKind.Individual,
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
                ?? ContactPreference.InApp);

        if (contact.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(contact.Error);
        }

        var slug = await _slugs.AllocateSellerSlugAsync(
            request.Slug, identity.Value.DisplayName, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(slug.Error);
        }

        var profile = SellerProfile.Create(
            SellerProfileId.From(_guids.NewSequentialGuid()),
            caller.Value,
            identity.Value,
            slug.Value,
            request.CountryCode,
            _clock.UtcNow);

        if (profile.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(profile.Error);
        }

        var contactApplied = profile.Value.UpdateProfile(
            identity.Value,
            contact.Value,
            request.CountryCode,
            _clock.UtcNow,
            baseRevision: null,
            caller.Value);

        if (contactApplied.IsFailure)
        {
            return Result.Failure<SellerProfileResponse>(contactApplied.Error);
        }

        if (request.ProviderId is { } providerId && providerId != Guid.Empty)
        {
            var linked = await LinkStorefrontAsync(
                profile.Value, providerId, cancellationToken);

            if (linked.IsFailure)
            {
                return Result.Failure<SellerProfileResponse>(linked.Error);
            }
        }

        await _sellers.AddAsync(profile.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SellerMapper.ToResponse(
            profile.Value, canManage: true, _authorization.IsStaff));
    }

    /// <summary>
    /// Links a Providers storefront, once B7 confirms it exists and that nobody else has claimed it.
    /// <para>
    /// <b>Ownership of the shop is B7's to answer and it does not publish that question</b>, so what
    /// this module can check is that the shop is real and that no other seller profile is already
    /// trading as it. That second check is the one that matters: it is what stops two members
    /// claiming the same shop's reputation.
    /// </para>
    /// </summary>
    private async Task<Result> LinkStorefrontAsync(
        SellerProfile profile, Guid providerId, CancellationToken cancellationToken)
    {
        if (!await _providers.ExistsAsync(providerId, cancellationToken))
        {
            return Result.Failure(MarketplaceErrors.StorefrontProviderUnknown);
        }

        var claimed = await _sellers.GetByProviderAsync(providerId, cancellationToken);

        if (claimed is not null && claimed.Id != profile.Id)
        {
            return Result.Failure(MarketplaceErrors.StorefrontNotOwned);
        }

        return profile.LinkStorefront(providerId, baseRevision: null);
    }
}
