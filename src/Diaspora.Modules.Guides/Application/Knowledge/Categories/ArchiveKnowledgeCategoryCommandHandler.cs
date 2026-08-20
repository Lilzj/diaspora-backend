using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Archives a navigation row, through the platform's soft-delete path.
/// <para>
/// <b>The handler never stamps the archival.</b> It marks the aggregate for removal and saves; the
/// shared context converts that into a soft delete, sets the timestamp and the actor, and leaves the
/// row where every audit and every restore can still find it. Stamping it by hand is the defect
/// B1.0.1 records, and it is silent — a hand-stamped delete looks correct in the response either
/// way.
/// </para>
/// <para>
/// <b>The in-use check is the interesting part.</b> A row a country still lists cannot be archived,
/// because the country page would quietly lose a line: the entry would resolve to nothing, the
/// composer would drop it, and nobody would be able to account for the gap. Refusing with the list
/// of countries turns that into a two-minute fix instead of a hunt.
/// </para>
/// </summary>
public sealed class ArchiveKnowledgeCategoryCommandHandler
    : ICommandHandler<ArchiveKnowledgeCategoryCommand>
{
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnowledgeCache _cache;

    public ArchiveKnowledgeCategoryCommandHandler(
        IKnowledgeCategoryRepository categories,
        ICountryKnowledgeDirectoryRepository directories,
        IUnitOfWork unitOfWork,
        IKnowledgeCache cache)
    {
        _categories = categories;
        _directories = directories;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result> Handle(
        ArchiveKnowledgeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByKeyAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure(KnowledgeErrors.CategoryNotFound);
        }

        var countries = await _directories.CountriesListingCategoryAsync(
            category.Key, cancellationToken);

        if (countries.Count > 0)
        {
            return Result.Failure(KnowledgeErrors.CategoryInUse);
        }

        _categories.Remove(category);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCatalogueAsync(cancellationToken);

        return Result.Success();
    }
}
