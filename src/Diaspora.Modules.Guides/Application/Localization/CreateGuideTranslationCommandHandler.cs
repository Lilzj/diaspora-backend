using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Creates the translation draft and seeds it from the source.
/// <para>
/// The structure is copied through the snapshot serializer rather than by walking the source's
/// entity graph: it is the one projection of a document that already exists, it is what versions
/// and auto-saves use, and reimplementing the walk here would be a second definition of what a
/// guide's content <em>is</em>.
/// </para>
/// <para>
/// The seeded text is the source's, untranslated, on purpose. A translator works from the
/// original in place, and an empty draft would make them keep two windows open to do it.
/// </para>
/// </summary>
public sealed class CreateGuideTranslationCommandHandler
    : ICommandHandler<CreateGuideTranslationCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideSlugAllocator _slugs;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateGuideTranslationCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideSlugAllocator slugs,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _slugs = slugs;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideResponse>> Handle(
        CreateGuideTranslationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var source = await _guides.GetForReadAsync(
            GuideId.From(command.GuideId), cancellationToken);

        if (source is null || !_authorization.CanSee(source))
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var request = command.Request;

        var language = GuideLanguage.Create(request.Language);

        if (language.IsFailure)
        {
            return Result.Failure<GuideResponse>(language.Error);
        }

        // Translating a guide into the language it is already written in is the one duplicate
        // this command can see without a group-wide read.
        if (source.Language.Equals(language.Value))
        {
            return Result.Failure<GuideResponse>(GuideErrors.TranslationExists);
        }

        var title = GuideTitle.Create(
            string.IsNullOrWhiteSpace(request.Title) ? source.Title.Value : request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<GuideResponse>(title.Error);
        }

        var slug = await _slugs.AllocateAsync(request.Slug, title.Value, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<GuideResponse>(slug.Error);
        }

        var translation = Guide.Create(
            GuideId.From(_guids.NewSequentialGuid()),
            caller.Value,
            title.Value,
            slug.Value,
            source.CategoryKey,
            language.Value,
            source.Location,
            _clock.UtcNow);

        if (translation.IsFailure)
        {
            return Result.Failure<GuideResponse>(translation.Error);
        }

        var linked = translation.Value.LinkTranslation(source.TranslationGroupId);

        if (linked.IsFailure)
        {
            return Result.Failure<GuideResponse>(linked.Error);
        }

        var seeded = SeedStructure(translation.Value, source, caller.Value);

        if (seeded.IsFailure)
        {
            return Result.Failure<GuideResponse>(seeded.Error);
        }

        await _guides.AddAsync(translation.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(translation.Value, forAuthor: true));
    }

    /// <summary>
    /// Copies the source's document onto the new draft. A source with nothing written yet is
    /// left alone rather than refused — starting a translation before the original is finished
    /// is a reasonable thing to do, and the aggregate refuses an empty structure by design.
    /// </summary>
    private static Result SeedStructure(
        Guide translation, Guide source, SharedKernel.Identity.UserId author)
    {
        if (source.Sections.Count == 0)
        {
            return Result.Success();
        }

        var snapshot = GuideSnapshotSerializer.Deserialize(
            GuideSnapshotSerializer.Serialize(source));

        if (snapshot.IsFailure)
        {
            return Result.Failure(snapshot.Error);
        }

        var sections = GuideSnapshotSerializer.ToDrafts(snapshot.Value);

        if (sections.IsFailure)
        {
            return Result.Failure(sections.Error);
        }

        return translation.ReplaceStructure(sections.Value, baseRevision: null, author);
    }
}
