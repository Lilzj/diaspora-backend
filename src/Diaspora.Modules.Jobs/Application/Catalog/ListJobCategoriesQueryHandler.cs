using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Catalog;

/// <summary>
/// Reads the category catalogue.
/// <para>
/// Read whole rather than paged: it is a few dozen rows that change when code ships, and a
/// caching decorator can sit in front of the repository without any of this changing.
/// </para>
/// </summary>
public sealed class ListJobCategoriesQueryHandler
    : IQueryHandler<ListJobCategoriesQuery, IReadOnlyList<JobCategoryResponse>>
{
    private readonly IJobCategoryRepository _categories;

    public ListJobCategoriesQueryHandler(IJobCategoryRepository categories) =>
        _categories = categories;

    public async Task<Result<IReadOnlyList<JobCategoryResponse>>> Handle(
        ListJobCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = await _categories.GetAllAsync(
            query.IncludeInactive, cancellationToken);

        return Result.Success<IReadOnlyList<JobCategoryResponse>>(
        [
            .. categories.Select(category => new JobCategoryResponse
            {
                CategoryId = category.Id.Value,
                Key = category.Key,
                Name = category.Name,
                Description = category.Description,
                ParentKey = category.ParentKey,
                IconKey = category.IconKey,
                Order = category.Order,
                IsActive = category.IsActive,
                PostingCount = category.PostingCount
            })
        ]);
    }
}
