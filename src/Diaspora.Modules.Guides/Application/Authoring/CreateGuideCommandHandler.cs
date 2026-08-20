using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

// The kernel's legacy identifier bag also declares a GuideId; the module-local one is
// authoritative for B8.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Creates a guide.
/// <para>
/// Every step is load-bearing: resolve the caller, hold them to the per-author draft ceiling,
/// validate the category against the catalogue, allocate a free deep-link handle, denormalise the
/// jurisdiction, and build the aggregate. The ceiling is an abuse bound rather than an editorial
/// one — it is drafts a script can create for free, not published guides, that are counted.
/// </para>
/// </summary>
public sealed class CreateGuideCommandHandler : ICommandHandler<CreateGuideCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideCategoryResolver _categories;
    private readonly GuideSlugAllocator _slugs;
    private readonly IOptions<GuideOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateGuideCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideCategoryResolver categories,
        GuideSlugAllocator slugs,
        IOptions<GuideOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _slugs = slugs;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideResponse>> Handle(
        CreateGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var request = command.Request;

        var authored = await _guides.CountByAuthorAsync(caller.Value, cancellationToken);

        if (authored >= _options.Value.MaxDraftsPerAuthor)
        {
            return Result.Failure<GuideResponse>(GuideErrors.TooManyGuides);
        }

        var title = GuideTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<GuideResponse>(title.Error);
        }

        var category = await _categories.ResolveActiveAsync(request.CategoryKey, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<GuideResponse>(category.Error);
        }

        var language = GuideLanguage.Create(request.Language);

        if (language.IsFailure)
        {
            return Result.Failure<GuideResponse>(language.Error);
        }

        var location = GuideLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName, request.AreaId);

        if (location.IsFailure)
        {
            return Result.Failure<GuideResponse>(location.Error);
        }

        var slug = await _slugs.AllocateAsync(request.Slug, title.Value, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<GuideResponse>(slug.Error);
        }

        var guide = Guide.Create(
            GuideId.From(_guids.NewSequentialGuid()),
            caller.Value,
            title.Value,
            slug.Value,
            category.Value.Key,
            language.Value,
            location.Value,
            _clock.UtcNow);

        if (guide.IsFailure)
        {
            return Result.Failure<GuideResponse>(guide.Error);
        }

        var summarized = ApplySummary(guide.Value, request.Summary, location.Value);

        if (summarized.IsFailure)
        {
            return Result.Failure<GuideResponse>(summarized.Error);
        }

        await _guides.AddAsync(guide.Value, cancellationToken);

        // The transaction behaviour opens and commits; it does not save. A handler that forgets
        // this returns a 201 describing a guide that was never written.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide.Value, forAuthor: true));
    }

    /// <summary>
    /// Applies the optional abstract the creation form collects. The aggregate's constructor
    /// deliberately does not take one — a summary is metadata, and metadata has exactly one
    /// mutator so the change event is raised in one place.
    /// </summary>
    private static Result ApplySummary(
        Guide guide, string? summary, GuideLocationContext location)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return Result.Success();
        }

        var created = GuideSummary.Create(summary);

        if (created.IsFailure)
        {
            return Result.Failure(created.Error);
        }

        return guide.UpdateMetadata(
            guide.Title,
            created.Value,
            guide.Seo,
            guide.Visibility,
            guide.Reading.Difficulty,
            guide.Reading.EstimatedCompletionMinutes,
            location,
            baseRevision: null,
            guide.AuthorId);
    }
}
