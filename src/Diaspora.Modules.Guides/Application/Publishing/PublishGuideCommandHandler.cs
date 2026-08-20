using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>
/// Publishes a guide and mints the version that records it.
/// <para>
/// <b>The order is load-bearing: publish first, snapshot second.</b> Publication is what bumps
/// the version number and computes the reading time from the word count, so a snapshot taken
/// before it would record the previous version's numbers against the new content. This is the
/// same sequence the scheduled publisher runs — one definition of what publication writes,
/// however it is triggered.
/// </para>
/// </summary>
public sealed class PublishGuideCommandHandler : ICommandHandler<PublishGuideCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVersionRepository _versions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PublishGuideCommandHandler(
        IGuideRepository guides,
        IGuideVersionRepository versions,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _versions = versions;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideResponse>> Handle(
        PublishGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

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

        var now = _clock.UtcNow;
        var published = guide.Publish(now, caller.Value);

        if (published.IsFailure)
        {
            return Result.Failure<GuideResponse>(published.Error);
        }

        var version = GuideVersion.Create(
            GuideVersionId.From(_guids.NewSequentialGuid()),
            guide.Id,
            guide.CurrentVersion,
            guide.Title,
            guide.Summary?.Summary,
            guide.Language,
            guide.CategoryKey,
            GuideSnapshotSerializer.Serialize(guide),
            command.Request.RevisionNotes,
            caller.Value,
            now,
            guide.WordCount,
            guide.Reading.ReadingTimeMinutes);

        if (version.IsFailure)
        {
            return Result.Failure<GuideResponse>(version.Error);
        }

        await _versions.AddAsync(version.Value, cancellationToken);

        // The transaction behaviour opens and commits; it does not save.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        GuideTelemetry.GuidesPublished.Add(1);
        GuideTelemetry.GuideVersionsCreated.Add(1);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
