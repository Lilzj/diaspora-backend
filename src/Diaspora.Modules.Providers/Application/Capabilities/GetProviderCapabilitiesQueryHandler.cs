using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Extensibility.Ranking;
using Diaspora.Modules.Providers.Extensibility.Types;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Capabilities;

/// <summary>
/// Describes the deployment.
/// <para>
/// Everything here is composed from registries and domain constants rather than hard-coded, so
/// a deployment that contributes a provider type, a ranking strategy or a stricter registration
/// rule sees it appear in the client's wizard without an app release. That is the whole reason
/// the endpoint exists.
/// </para>
/// </summary>
public sealed class GetProviderCapabilitiesQueryHandler
    : IQueryHandler<GetProviderCapabilitiesQuery, ProviderCapabilitiesResponse>
{
    private readonly ProviderTypeRegistry _types;
    private readonly RankingStrategyRegistry _rankings;

    public GetProviderCapabilitiesQueryHandler(
        ProviderTypeRegistry types, RankingStrategyRegistry rankings)
    {
        _types = types;
        _rankings = rankings;
    }

    public Task<Result<ProviderCapabilitiesResponse>> Handle(
        GetProviderCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = new ProviderCapabilitiesResponse
        {
            ProviderTypes = _types.Available.Select(ToOption).ToList(),
            DeliveryModes = Names<ServiceDeliveryMode>(),
            PricingModels = Names<PricingModel>(),
            ContactMethodKinds = Names<ContactMethodKind>(),
            AvailabilityKinds = Names<AvailabilityKind>(),
            VisibilityLevels = Names<ProviderVisibilityLevel>(),
            SortOrders = Names<ProviderSortOrder>(),
            RankingKeys = _rankings.Keys.ToList(),
            MediaRoles =
            [
                ProviderMediaRole.Cover,
                ProviderMediaRole.Logo,
                ProviderMediaRole.Gallery,
                ProviderMediaRole.Menu,
                ProviderMediaRole.PriceList,
                ProviderMediaRole.Brochure,
                ProviderMediaRole.Certificate,
                ProviderMediaRole.Document,
                ProviderMediaRole.Video
            ],
            MaxServices = ProviderLimits.MaxServices,
            MaxGalleryItems = ProviderLimits.MaxGalleryItems,
            MaxTags = ProviderLimits.MaxTags,
            MaxServiceAreas = ProviderLimits.MaxServiceAreas,
            MaxLanguages = ProviderLimits.MaxLanguages,
            MaxContactMethods = ProviderLimits.MaxContactMethods,
            MaxCertifications = ProviderLimits.MaxCertifications,
            MaxReviewBodyLength = ProviderLimits.MaxReviewBodyLength,
            ReviewEditWindowDays = (int)ProviderLimits.ReviewEditWindow.TotalDays,
            MaxPageSize = ProviderLimits.MaxPageSize
        };

        return Task.FromResult(Result.Success(response));
    }

    private static ProviderTypeOptionResponse ToOption(ProviderTypeDescriptor descriptor) => new()
    {
        Type = ProviderWireNames.ToWire(descriptor.Type),
        Label = descriptor.Label,
        Description = descriptor.Description,
        Icon = descriptor.Icon,
        RequiresRegistrationNumber = descriptor.RequiresRegistrationNumber,
        SupportsOrganizationDetail = descriptor.SupportsOrganizationDetail,
        VerificationKinds = descriptor.VerificationKinds
            .Select(ProviderWireNames.ToWire)
            .ToList(),
        DefaultDeliveryMode = ProviderWireNames.ToWire(descriptor.DefaultDeliveryMode)
    };

    private static string[] Names<TEnum>() where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>();
}
