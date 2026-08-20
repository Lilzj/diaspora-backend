using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Options;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Mints an access token, bound to a variant and optionally to one member.</summary>
public sealed class CreateSignedUrlCommandHandler
    : ICommandHandler<CreateSignedUrlCommand, SignedUrlResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly SignedUrlFactory _signedUrls;
    private readonly MediaStorageOptions _options;

    public CreateSignedUrlCommandHandler(
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        SignedUrlFactory signedUrls,
        IOptions<MediaStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _assets = assets;
        _guard = guard;
        _signedUrls = signedUrls;
        _options = options.Value;
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

        var token = _signedUrls.Create(asset.Id, purpose, audience, asset.Kind);

        var path = purpose is MediaVariantPurpose.Original
            ? $"/api/v1/media/{asset.Id.Value}/content"
            : $"/api/v1/media/{asset.Id.Value}/variants/{purpose.ToString().ToLowerInvariant()}";

        var url = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{path}?{token.ToQueryString()}"
            : $"{_options.PublicBaseUrl.TrimEnd('/')}{path}?{token.ToQueryString()}";

        return Result.Success(new SignedUrlResponse
        {
            MediaId = asset.Id.Value,
            Url = url,
            Variant = MediaWireNames.ToWire(purpose),
            ExpiresAt = token.ExpiresAt
        });
    }
}
