using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Saves the editor's document.
/// <para>
/// <b>The concurrency check happens once, up front.</b> A save can touch both metadata and
/// structure, and each mutation bumps the revision — so handing the client's base revision to the
/// second mutator would make every combined save conflict with itself. The guard runs once
/// against the revision the client loaded, and the mutators are then called with the check opted
/// out because the handler has already answered the question.
/// </para>
/// <para>
/// Assets referenced by the saved blocks are claimed from Media afterwards. A claim that fails is
/// logged and the save stands — an unclaimed asset is a lost image, a refused save is a lost
/// afternoon.
/// </para>
/// </summary>
public sealed class UpdateGuideCommandHandler : ICommandHandler<UpdateGuideCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideMediaClaimer _media;

    public UpdateGuideCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideMediaClaimer media)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _media = media;
    }

    public async Task<Result<GuideResponse>> Handle(
        UpdateGuideCommand command, CancellationToken cancellationToken)
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

        var request = command.Request;

        var current = guide.EnsureRevision(request.BaseRevision);

        if (current.IsFailure)
        {
            return Result.Failure<GuideResponse>(current.Error);
        }

        var title = GuideTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<GuideResponse>(title.Error);
        }

        var summary = GuideSummary.Create(request.Summary);

        if (summary.IsFailure)
        {
            return Result.Failure<GuideResponse>(summary.Error);
        }

        var seo = GuideSeoMetadata.Create(
            request.MetaTitle, request.MetaDescription, request.CanonicalUrl, request.Keywords);

        if (seo.IsFailure)
        {
            return Result.Failure<GuideResponse>(seo.Error);
        }

        // The area id old app builds still send is accepted and not written (14 Oct 2026): a guide
        // is found by its country, the town its author typed and its pin.
        var location = GuideLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName, areaId: null);

        if (location.IsFailure)
        {
            return Result.Failure<GuideResponse>(location.Error);
        }

        var visibility = GuideWireNames.ParseVisibility(request.Visibility) ?? guide.Visibility;
        var difficulty = GuideWireNames.ParseDifficulty(request.Difficulty)
            ?? guide.Reading.Difficulty;

        IReadOnlyList<GuideSectionDraft>? structure = null;

        if (request.Sections is { } sections)
        {
            var drafts = GuideStructureInputMapper.ToDrafts(sections);

            if (drafts.IsFailure)
            {
                return Result.Failure<GuideResponse>(drafts.Error);
            }

            structure = drafts.Value;
        }

        var metadata = guide.UpdateMetadata(
            title.Value,
            summary.Value,
            seo.Value,
            visibility,
            difficulty,
            request.EstimatedCompletionMinutes,
            location.Value,
            baseRevision: null,
            caller.Value);

        if (metadata.IsFailure)
        {
            return Result.Failure<GuideResponse>(metadata.Error);
        }

        // Omitted coordinates leave the pin alone, so a client that has never sent one cannot
        // clear a pin a newer client set.
        if (request.Latitude is not null || request.Longitude is not null)
        {
            var pinned = guide.SetCoordinates(request.Latitude, request.Longitude);

            if (pinned.IsFailure)
            {
                return Result.Failure<GuideResponse>(pinned.Error);
            }
        }

        if (structure is not null)
        {
            var replaced = guide.ReplaceStructure(structure, baseRevision: null, caller.Value);

            if (replaced.IsFailure)
            {
                return Result.Failure<GuideResponse>(replaced.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (structure is not null)
        {
            await ClaimAsync(guide, structure, cancellationToken);
        }

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }

    /// <summary>
    /// Tells Media the saved document is using its uploads. Best effort by design — see
    /// <see cref="GuideMediaClaimer"/> for why a claim never fails a write.
    /// </summary>
    private async Task ClaimAsync(
        Guide guide,
        IReadOnlyList<GuideSectionDraft> sections,
        CancellationToken cancellationToken)
    {
        var position = 0;

        foreach (var mediaId in GuideStructureInputMapper.MediaIds(sections))
        {
            await _media.ClaimAsync(
                guide.Id.Value,
                mediaId,
                nameof(GuideMediaRole.Embedded),
                position++,
                cancellationToken);
        }
    }
}
