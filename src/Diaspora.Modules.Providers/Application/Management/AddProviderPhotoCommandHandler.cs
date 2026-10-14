using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// <c>POST /providers/{id}/photos</c>.
/// <para>
/// <b>Added 14 Oct 2026.</b> A provider's photographs had nowhere to go: the gallery, cover and
/// logo existed on the aggregate and in the response, and no route set any of them. The app
/// uploaded each photo with the provider as its owner and stopped there, so every business was
/// published with no picture.
/// </para>
/// <para>
/// The asset is attached through Media first. That is Media's own access check, so a member can
/// only put on their provider a photograph they uploaded; and it is the claim that keeps Media
/// from sweeping the asset as unused. A <c>cover</c> becomes the card and header image, and the
/// first photograph becomes the cover when there is none yet.
/// </para>
/// </summary>
public sealed class AddProviderPhotoCommandHandler
    : ICommandHandler<AddProviderPhotoCommand, ProviderResponse>
{
    private readonly IProviderRepository _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IGuidGenerator _guids;
    private readonly ProviderMediaClaimer _media;

    public AddProviderPhotoCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IGuidGenerator guids,
        ProviderMediaClaimer media)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
        _guids = guids;
        _media = media;
    }

    public async Task<Result<ProviderResponse>> Handle(
        AddProviderPhotoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var caller = _authorization.RequireRole(provider, ProviderRole.Editor);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var isCover = string.Equals(
            command.Request.Role, ProviderMediaRole.Cover, StringComparison.OrdinalIgnoreCase);

        var attached = await _media.AttachAsync(
            provider.Id.Value,
            command.Request.MediaId,
            isCover ? ProviderMediaRole.Cover : ProviderMediaRole.Gallery,
            provider.Gallery.Count,
            cancellationToken);

        if (attached.IsFailure)
        {
            return Result.Failure<ProviderResponse>(attached.Error);
        }

        var mediaId = MediaId.From(command.Request.MediaId);

        var added = provider.AddGalleryItem(ProviderGalleryItem.Create(
            ProviderGalleryItemId.From(_guids.NewGuid()),
            mediaId,
            ProviderMediaRole.Gallery,
            caption: command.Request.Caption));

        if (added.IsFailure)
        {
            return Result.Failure<ProviderResponse>(added.Error);
        }

        if (isCover || provider.Profile.CoverMediaId is null)
        {
            provider.SetCoverMedia(mediaId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
