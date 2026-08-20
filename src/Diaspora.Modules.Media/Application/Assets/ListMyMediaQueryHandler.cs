using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Lists the caller's own assets.</summary>
public sealed class ListMyMediaQueryHandler
    : IQueryHandler<ListMyMediaQuery, PagedResult<MediaAssetResponse>>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public ListMyMediaQueryHandler(
        IMediaAssetRepository assets, MediaMapper mapper, ICurrentUser currentUser)
    {
        _assets = assets;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<MediaAssetResponse>>> Handle(
        ListMyMediaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<PagedResult<MediaAssetResponse>>(MediaErrors.NotAuthenticated);
        }

        // An unrecognised kind filters to nothing rather than failing. A library screen with a
        // stale filter should show an empty list, not an error.
        MediaKind? kind = string.IsNullOrWhiteSpace(request.Kind)
            ? null
            : MediaWireNames.ParseOrDefault(request.Kind, MediaKind.Unknown);

        var page = await _assets.ListForUserAsync(
            userId, kind, request.Pagination.Sanitize(), cancellationToken);

        return Result.Success(page.Map(_mapper.ToResponse));
    }
}
