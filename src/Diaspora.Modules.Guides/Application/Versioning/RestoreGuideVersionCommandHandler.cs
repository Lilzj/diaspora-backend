using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// Copies a published version's content back onto the draft.
/// <para>
/// The version stores its structure as an opaque snapshot, so the restore round-trips it through
/// the same serializer that wrote it. A snapshot that will not deserialize — or that names a
/// block kind this deployment no longer ships — fails the whole restore rather than dropping
/// what it cannot read: a half-restored document written under a success code is a corruption
/// the author only discovers on reopening it.
/// </para>
/// </summary>
public sealed class RestoreGuideVersionCommandHandler
    : ICommandHandler<RestoreGuideVersionCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVersionRepository _versions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAnalyticsRecorder _analytics;

    public RestoreGuideVersionCommandHandler(
        IGuideRepository guides,
        IGuideVersionRepository versions,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideAnalyticsRecorder analytics)
    {
        _guides = guides;
        _versions = versions;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _analytics = analytics;
    }

    public async Task<Result<GuideResponse>> Handle(
        RestoreGuideVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var current = guide.EnsureRevision(command.Request.BaseRevision);

        if (current.IsFailure)
        {
            return Result.Failure<GuideResponse>(current.Error);
        }

        var version = await _versions.GetByNumberAsync(
            guideId, command.VersionNumber, cancellationToken);

        if (version is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.VersionNotFound);
        }

        var snapshot = GuideSnapshotSerializer.Deserialize(version.ContentSnapshot);

        if (snapshot.IsFailure)
        {
            return Result.Failure<GuideResponse>(snapshot.Error);
        }

        var sections = GuideSnapshotSerializer.ToDrafts(snapshot.Value);

        if (sections.IsFailure)
        {
            return Result.Failure<GuideResponse>(sections.Error);
        }

        var title = GuideTitle.Create(version.Title);

        if (title.IsFailure)
        {
            return Result.Failure<GuideResponse>(title.Error);
        }

        var summary = GuideSummary.Create(version.Summary);

        if (summary.IsFailure)
        {
            return Result.Failure<GuideResponse>(summary.Error);
        }

        var language = GuideLanguage.Create(version.Language);

        if (language.IsFailure)
        {
            return Result.Failure<GuideResponse>(language.Error);
        }

        var restored = guide.RestoreVersion(
            title.Value,
            summary.Value,
            language.Value,
            version.CategoryKey,
            sections.Value,

            // Already checked above; passing it again would compare against a revision this
            // handler has not yet bumped but is about to.
            baseRevision: null,
            caller.Value);

        if (restored.IsFailure)
        {
            return Result.Failure<GuideResponse>(restored.Error);
        }

        await _analytics.RecordVersionAdoptionAsync(guideId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
