using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Mints an access token, bound to a variant and optionally to one member.</summary>
public sealed class CreateSignedUrlCommandHandler
    : ICommandHandler<CreateSignedUrlCommand, SignedUrlResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly MediaUrlFactory _urls;

    public CreateSignedUrlCommandHandler(
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        MediaUrlFactory urls)
    {
        _assets = assets;
        _guard = guard;
        _urls = urls;
    }

    public async Task<Result<SignedUrlResponse>> Handle(
        CreateSignedUrlCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<SignedUrlResponse>(MediaErrors.AssetNotFound);
        }

        // The caller must be able to read the content before they can mint a link to it.
        // Anything weaker would make this endpoint a way to launder access.
        var allowed = await _guard.CanReadContentAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return Result.Failure<SignedUrlResponse>(MediaErrors.AssetNotFound);
        }

        var purpose = MediaWireNames.ParseOrDefault(
            request.Request.Variant, MediaVariantPurpose.Original);

        if (purpose is not MediaVariantPurpose.Original && !asset.HasVariant(purpose))
        {
            return Result.Failure<SignedUrlResponse>(MediaErrors.VariantNotFound);
        }

        var audience = request.Request.AudienceUserId is { } id ? UserId.From(id) : null;

        // Built by the same factory every other media URL comes from, rather than assembled
        // here. The two copies had already drifted in the way that mattered: this one emitted a
        // bare `/api/v1/...` path whenever `PublicBaseUrl` was unset, and a mobile client joining
        // that onto its own base cannot resolve it.
        var link = _urls.ForSignedVariant(asset, purpose, audience);

        return Result.Success(new SignedUrlResponse
        {
            MediaId = asset.Id.Value,
            Url = link.Url,
            Variant = MediaWireNames.ToWire(purpose),
            ExpiresAt = link.ExpiresAt
        });
    }
}
