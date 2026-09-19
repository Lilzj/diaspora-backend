using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>
/// Reads the quarantine.
/// <para>
/// No access guard, unlike every other read in this module: the whole point of the permission on
/// the query is that this caller sees files nobody else may, including files belonging to members
/// they have no other relationship with. Asking the guard would filter the queue down to the
/// moderator's own uploads, which is the one answer that cannot be useful.
/// </para>
/// </summary>
public sealed class ListQuarantinedMediaQueryHandler
    : IQueryHandler<ListQuarantinedMediaQuery, IReadOnlyList<QuarantinedMediaResponse>>
{
    private const int DefaultLimit = 50;

    private const int MaximumLimit = 200;

    private readonly IMediaAssetRepository _assets;

    public ListQuarantinedMediaQueryHandler(IMediaAssetRepository assets) => _assets = assets;

    public async Task<Result<IReadOnlyList<QuarantinedMediaResponse>>> Handle(
        ListQuarantinedMediaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaximumLimit);

        var assets = await _assets.FindQuarantinedAsync(limit, cancellationToken);

        return Result.Success<IReadOnlyList<QuarantinedMediaResponse>>(
            [.. assets.Select(ToResponse)]);
    }

    private static QuarantinedMediaResponse ToResponse(MediaAsset asset) =>
        new()
        {
            MediaId = asset.Id.Value,
            FileName = asset.FileName.Value,
            ContentType = asset.ContentType.Value,
            Kind = asset.Kind.ToString(),
            SizeBytes = asset.Size.Bytes,
            UploadedBy = asset.UploadedBy.Value,
            ScanStatus = asset.ScanResult.Status.ToString(),
            Scanner = asset.ScanResult.Scanner,
            ThreatName = asset.ScanResult.ThreatName,
            ScannedAt = asset.ScanResult.ScannedAt,
            OwnerType = asset.Owner.Type.ToString(),
            OwnerId = asset.Owner.Type == MediaOwnerType.Unassigned ? null : asset.Owner.Id,
            CreatedAt = asset.CreatedAt
        };
}
