using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Resolves a category key against the catalogue.
/// <para>
/// One place, because "does this category exist and is it still in use" is asked by creation,
/// by editing and by the category-change command — and three copies would eventually differ on
/// whether an inactive category is acceptable.
/// </para>
/// <para>
/// <b>An inactive category is refused for a new posting and tolerated on an existing one.</b>
/// Retiring a category must not make every posting under it unsaveable — that would turn an
/// administrative tidy-up into an outage for a hundred employers.
/// </para>
/// </summary>
public sealed class JobCategoryResolver
{
    private readonly IJobCategoryRepository _categories;

    public JobCategoryResolver(IJobCategoryRepository categories) => _categories = categories;

    /// <summary>The category, if it exists and is still in use. For a new or re-categorised posting.</summary>
    public async Task<Result<JobCategory>> ResolveActiveAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        var category = await ResolveAsync(key, cancellationToken);

        if (category.IsFailure)
        {
            return category;
        }

        return category.Value.IsActive
            ? category
            : Result.Failure<JobCategory>(JobErrors.CategoryInactive);
    }

    /// <summary>
    /// The category whether or not it is still in use. For a posting that already carries it.
    /// </summary>
    public async Task<Result<JobCategory>> ResolveAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        var normalized = JobCategory.NormalizeKey(key);

        if (normalized is null)
        {
            return Result.Failure<JobCategory>(JobErrors.CategoryKeyInvalid);
        }

        var category = await _categories.GetByKeyAsync(normalized, cancellationToken);

        return category is null
            ? Result.Failure<JobCategory>(JobErrors.CategoryNotFound)
            : Result.Success(category);
    }
}
