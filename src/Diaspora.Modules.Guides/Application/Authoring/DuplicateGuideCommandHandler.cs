using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Copies a guide the caller owns into a new draft.
/// <para>
/// <b>What is copied, and what deliberately is not.</b> A duplicate is a starting point for new
/// writing, not a second copy of a published thing. Getting this list wrong is how a duplication
/// feature becomes a way to launder reputation.
/// </para>
/// <list type="table">
/// <item><term>Copied</term><description>title, summary, category, language, jurisdiction — the
/// editorial content the author wrote.</description></item>
/// <item><term>Not copied — authorship</term><description>the copy is authored by the
/// <em>caller</em>. Duplication is only offered to the owner today, so the two coincide; stating it
/// as the caller rather than the source's author means the rule stays right if that ever
/// widens.</description></item>
/// <item><term>Not copied — publication state</term><description>always a fresh draft. A copy that
/// arrived published would put unreviewed text in front of members through a route with no
/// moderation step.</description></item>
/// <item><term>Not copied — the deep-link handle</term><description>a new slug is allocated. Two
/// rows cannot hold one handle, and inheriting it would break the original's links.</description></item>
/// <item><term>Not copied — engagement, trust and version history</term><description>views, helpful
/// votes, the trust mark, the version chain and the first-published date all belong to the original.
/// Copying a trust mark in particular would let anybody mint a verified-looking guide from a
/// verified one.</description></item>
/// </list>
/// <para>
/// <b>The draft ceiling applies.</b> It is an abuse bound on rows a script can create for free, and
/// a duplicate route is exactly such a script — exempting it would leave the ceiling in place while
/// opening a door beside it.
/// </para>
/// </summary>
public sealed class DuplicateGuideCommandHandler
    : ICommandHandler<DuplicateGuideCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideSlugAllocator _slugs;
    private readonly IOptions<GuideOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public DuplicateGuideCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideSlugAllocator slugs,
        IOptions<GuideOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _slugs = slugs;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideResponse>> Handle(
        DuplicateGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var source = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (source is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        // Owner only. RequireAuthor also admits staff, which is right for moderation and wrong
        // here: a moderator duplicating somebody's guide would author a copy under their own name.
        if (source.AuthorId != caller.Value)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotAuthor);
        }

        var authored = await _guides.CountByAuthorAsync(caller.Value, cancellationToken);

        if (authored >= _options.Value.MaxDraftsPerAuthor)
        {
            return Result.Failure<GuideResponse>(GuideErrors.TooManyGuides);
        }

        // "Copy of …" rather than the bare title, because two identically named rows in the
        // author's own drafts list is the first thing that makes a duplicate feature annoying.
        var title = GuideTitle.Create(
            string.IsNullOrWhiteSpace(command.Title)
                ? Copied(source.Title.Value)
                : command.Title);

        if (title.IsFailure)
        {
            return Result.Failure<GuideResponse>(title.Error);
        }

        // Null asks the allocator for a fresh handle derived from the title; it de-duplicates.
        var slug = await _slugs.AllocateAsync(null, title.Value, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<GuideResponse>(slug.Error);
        }

        // ── Every value object is rebuilt. See DuplicateListingCommandHandler ──
        //
        // `Language`, `Location` and `Summary` are `OwnsOne` — owned entities
        // with an identifying foreign key back to their guide. Sharing a source's
        // tracked instance with a second aggregate makes EF try to re-parent it,
        // which it refuses with `… is part of a key and so cannot be modified`.
        var language = GuideLanguage.Create(source.Language.Code);

        if (language.IsFailure)
        {
            return Result.Failure<GuideResponse>(language.Error);
        }

        var location = GuideLocationContext.Create(
            source.Location.CountryCode, source.Location.RegionName, source.Location.CityName,
            areaId: null, areaPath: null);

        if (location.IsFailure)
        {
            return Result.Failure<GuideResponse>(location.Error);
        }

        var copy = Guide.Create(
            GuideId.From(_guids.NewSequentialGuid()),
            caller.Value,
            title.Value,
            slug.Value,
            source.CategoryKey,
            language.Value,
            location.Value,
            _clock.UtcNow);

        if (copy.IsFailure)
        {
            return Result.Failure<GuideResponse>(copy.Error);
        }

        if (source.Summary is { } sourceSummary)
        {
            var summary = GuideSummary.Create(sourceSummary.Summary, sourceSummary.Excerpt);

            if (summary.IsFailure)
            {
                return Result.Failure<GuideResponse>(summary.Error);
            }

            var applied = copy.Value.UpdateMetadata(
                copy.Value.Title,
                summary.Value,
                copy.Value.Seo,
                copy.Value.Visibility,
                copy.Value.Reading.Difficulty,
                copy.Value.Reading.EstimatedCompletionMinutes,
                copy.Value.Location,
                baseRevision: null,
                changedBy: caller.Value);

            if (applied.IsFailure)
            {
                return Result.Failure<GuideResponse>(applied.Error);
            }
        }

        await _guides.AddAsync(copy.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(copy.Value, forAuthor: true));
    }

    /// <summary>
    /// Prefixes once. Duplicating a duplicate gives "Copy of X" rather than "Copy of Copy of X",
    /// which is what an author actually wants on the third pass through a template.
    /// </summary>
    private static string Copied(string title) =>
        title.StartsWith("Copy of ", StringComparison.OrdinalIgnoreCase)
            ? title
            : $"Copy of {title}";
}
