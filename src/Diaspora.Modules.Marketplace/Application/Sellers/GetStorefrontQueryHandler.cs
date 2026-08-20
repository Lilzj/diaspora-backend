using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>
/// Reads a storefront.
/// <para>
/// <b>A closed profile resolves as not-found to everyone but its owner and staff.</b> A member who
/// closed their shop should not have it browsable; the row survives because completed sales point
/// at it, which is a different thing from it being a place buyers can visit.
/// </para>
/// <para>
/// The shop name behind a provider-backed storefront is B7's answer at read time. It fails soft: an
/// outage renders the storefront without the shop's current name rather than failing the page.
/// </para>
/// </summary>
public sealed class GetStorefrontQueryHandler
    : IQueryHandler<GetStorefrontQuery, SellerProfileResponse>
{
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly MarketplaceProviderResolver _providers;

    public GetStorefrontQueryHandler(
        ISellerProfileRepository sellers,
        MarketplaceAuthorizationService authorization,
        MarketplaceProviderResolver providers)
    {
        _sellers = sellers;
        _authorization = authorization;
        _providers = providers;
    }

    public async Task<Result<SellerProfileResponse>> Handle(
        GetStorefrontQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var seller = await ResolveAsync(query, cancellationToken);

        if (seller is null)
        {
            return Result.Failure<SellerProfileResponse>(MarketplaceErrors.SellerNotFound);
        }

        var isOwner = _authorization.CallerId == seller.OwnerUserId;

        if (seller.Status == SellerStatus.Closed && !isOwner && !_authorization.IsStaff)
        {
            return Result.Failure<SellerProfileResponse>(MarketplaceErrors.SellerNotFound);
        }

        Modules.Providers.Integration.ProviderSummary? provider = null;

        if (seller.ProviderId is { } providerId)
        {
            var resolved = await _providers.ResolveAsync([providerId], cancellationToken);
            provider = resolved.GetValueOrDefault(providerId);
        }

        return Result.Success(SellerMapper.ToResponse(
            seller, isOwner, _authorization.IsStaff, provider));
    }

    private async Task<SellerProfile?> ResolveAsync(
        GetStorefrontQuery query, CancellationToken cancellationToken)
    {
        if (query.SellerId is { } id)
        {
            return await _sellers.GetForReadAsync(
                SellerProfileId.From(id), cancellationToken);
        }

        var slug = ListingSlug.Create(query.Slug);

        return slug.IsFailure
            ? null
            : await _sellers.GetBySlugAsync(slug.Value, cancellationToken);
    }
}
