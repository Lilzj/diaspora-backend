using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// Serves one version's content.
/// <para>
/// The stored snapshot is deserialized here rather than in the mapper, so the mapper stays a
/// pure function and this handler owns what a corrupt row means: a
/// <see cref="GuideErrors.SnapshotInvalid"/> result, never an unhandled exception — one bad row
/// in a history is a failed request, not a failed process.
/// </para>
/// </summary>
public sealed class GetGuideVersionQueryHandler
    : IQueryHandler<GetGuideVersionQuery, GuideVersionResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVersionRepository _versions;
    private readonly GuideAuthorizationService _authorization;

    public GetGuideVersionQueryHandler(
        IGuideRepository guides,
        IGuideVersionRepository versions,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _versions = versions;
        _authorization = authorization;
    }

    public async Task<Result<GuideVersionResponse>> Handle(
        GetGuideVersionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guideId = GuideId.From(query.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideVersionResponse>(GuideErrors.NotFound);
        }

        var version = await _versions.GetByNumberAsync(
            guideId, query.VersionNumber, cancellationToken);

        if (version is null)
        {
            return Result.Failure<GuideVersionResponse>(GuideErrors.VersionNotFound);
        }

        var snapshot = GuideSnapshotSerializer.Deserialize(version.ContentSnapshot);

        if (snapshot.IsFailure)
        {
            return Result.Failure<GuideVersionResponse>(snapshot.Error);
        }

        return Result.Success(GuideVersionMapper.ToResponse(version, snapshot.Value));
    }
}
