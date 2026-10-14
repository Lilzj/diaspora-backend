using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Lets anyone who can see a provider see its photographs.
/// <para>
/// <b>Added 14 Oct 2026.</b> Uploads are private, and Media lets others read a private asset only
/// when the owning module says so. Housing and Marketplace said so; Providers had no policy, so a
/// business's photographs were visible to the member who uploaded them and nobody else. Same
/// rules as reading the provider itself (<c>GetProviderQueryHandler.CanSee</c>).
/// </para>
/// </summary>
public sealed class ProviderMediaPolicy : IMediaAccessPolicy
{
    private static readonly string[] PubliclyVisibleRoles =
    [
        MediaReferenceRoles.Cover,
        MediaReferenceRoles.Gallery,
        MediaReferenceRoles.Logo,
        MediaReferenceRoles.Banner
    ];

    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;

    public ProviderMediaPolicy(IProviderRepository providers, ProviderAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(authorization);

        _providers = providers;
        _authorization = authorization;
    }

    public MediaOwnerType OwnerType => MediaOwnerType.Provider;

    public async Task<MediaAccessDecision> EvaluateAsync(
        MediaAccessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestedLevel is not MediaAccessLevel.Read
            || !PubliclyVisibleRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            return MediaAccessDecision.Abstain();
        }

        var provider = await _providers.GetForReadAsync(ProviderId.From(request.OwnerId), cancellationToken);

        if (provider is null)
        {
            return MediaAccessDecision.Abstain();
        }

        if (_authorization.CanSeeUnpublished(provider))
        {
            return MediaAccessDecision.Allow("The caller manages this provider.");
        }

        var visible = !provider.IsDeleted
            && provider.Visibility.Level != ProviderVisibilityLevel.Private
            && provider.Status is not (ProviderStatus.Draft or ProviderStatus.Archived or ProviderStatus.Suspended)
            && (provider.Visibility.Level == ProviderVisibilityLevel.Public || _authorization.CallerId is not null);

        return visible
            ? MediaAccessDecision.Allow("The provider carrying this image is visible to the caller.")
            : MediaAccessDecision.Abstain();
    }
}
