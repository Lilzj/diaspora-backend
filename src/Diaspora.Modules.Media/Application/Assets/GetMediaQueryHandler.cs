using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Reads one asset, after the guard has agreed the caller may see it.</summary>
public sealed class GetMediaQueryHandler : IQueryHandler<GetMediaQuery, MediaAssetResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly MediaMapper _mapper;

    public GetMediaQueryHandler(
        IMediaAssetRepository assets, MediaAccessGuard guard, MediaMapper mapper)
    {
        _assets = assets;
        _guard = guard;
        _mapper = mapper;
    }

    public async Task<Result<MediaAssetResponse>> Handle(
        GetMediaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanReadMetadataAsync(asset, cancellationToken);

        // A refusal is reported as not-found rather than forbidden. Distinguishing them tells
        // a prober which identifiers exist, which is a free enumeration oracle over every
        // photograph on the platform.
        return allowed.IsFailure
            ? Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound)
            : Result.Success(_mapper.ToResponse(asset));
    }
}
