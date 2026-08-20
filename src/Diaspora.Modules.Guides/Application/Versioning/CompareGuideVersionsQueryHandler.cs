using Diaspora.Application.Abstractions;
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
/// Serves the version diff.
/// <para>
/// Both snapshots are deserialized and handed to <c>GuideVersionDiffService</c>, which is a pure
/// function of its inputs — the same two versions produce the same answer forever, which is what
/// makes this endpoint cacheable and its output quotable.
/// </para>
/// <para>
/// A version number that does not exist is a <see cref="GuideErrors.VersionNotFound"/> rather
/// than an empty diff: "nothing changed" and "you asked about a version that was never
/// published" are different answers, and only one of them is true.
/// </para>
/// </summary>
public sealed class CompareGuideVersionsQueryHandler
    : IQueryHandler<CompareGuideVersionsQuery, GuideVersionDiffResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVersionRepository _versions;
    private readonly GuideAuthorizationService _authorization;

    public CompareGuideVersionsQueryHandler(
        IGuideRepository guides,
        IGuideVersionRepository versions,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _versions = versions;
        _authorization = authorization;
    }

    public async Task<Result<GuideVersionDiffResponse>> Handle(
        CompareGuideVersionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guideId = GuideId.From(query.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideVersionDiffResponse>(GuideErrors.NotFound);
        }

        var from = await _versions.GetByNumberAsync(
            guideId, query.FromVersion, cancellationToken);

        var to = await _versions.GetByNumberAsync(guideId, query.ToVersion, cancellationToken);

        if (from is null || to is null)
        {
            return Result.Failure<GuideVersionDiffResponse>(GuideErrors.VersionNotFound);
        }

        var fromSnapshot = GuideSnapshotSerializer.Deserialize(from.ContentSnapshot);

        if (fromSnapshot.IsFailure)
        {
            return Result.Failure<GuideVersionDiffResponse>(fromSnapshot.Error);
        }

        var toSnapshot = GuideSnapshotSerializer.Deserialize(to.ContentSnapshot);

        if (toSnapshot.IsFailure)
        {
            return Result.Failure<GuideVersionDiffResponse>(toSnapshot.Error);
        }

        return Result.Success(GuideVersionDiffService.Diff(
            guide.Id.Value,
            from.VersionNumber,
            fromSnapshot.Value,
            to.VersionNumber,
            toSnapshot.Value));
    }
}
