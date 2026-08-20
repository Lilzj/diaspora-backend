using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Looks up assets by content digest, returning only those the caller may already see.
/// <para>
/// The filter is not optional. Without it, this endpoint answers "does a file with this exact
/// content exist on the platform" for any caller holding the bytes — which for a leaked
/// document is precisely the confirmation an attacker wants.
/// </para>
/// </summary>
public sealed class FindMediaByHashQueryHandler
    : IQueryHandler<FindMediaByHashQuery, IReadOnlyList<MediaAssetResponse>>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly MediaMapper _mapper;

    public FindMediaByHashQueryHandler(
        IMediaAssetRepository assets, MediaAccessGuard guard, MediaMapper mapper)
    {
        _assets = assets;
        _guard = guard;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<MediaAssetResponse>>> Handle(
        FindMediaByHashQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hash = ContentHash.TryCreate(request.ContentHash);

        if (hash.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MediaAssetResponse>>(hash.Error);
        }

        var matches = await _assets.FindByHashAsync(hash.Value, cancellationToken);
        var visible = new List<MediaAssetResponse>();

        foreach (var asset in matches)
        {
            var allowed = await _guard.CanReadMetadataAsync(asset, cancellationToken);

            if (allowed.IsSuccess)
            {
                visible.Add(_mapper.ToResponse(asset));
            }
        }

        return Result.Success<IReadOnlyList<MediaAssetResponse>>(visible);
    }
}
