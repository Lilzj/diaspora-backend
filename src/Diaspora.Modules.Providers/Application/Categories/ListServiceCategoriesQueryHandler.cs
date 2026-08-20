using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Categories;

/// <summary>
/// Serves the category catalogue.
/// <para>
/// Retired categories are returned only to staff. A member offered a retired category would file
/// a provider under something nobody browses, and would have no way to know why their traffic
/// never arrived.
/// </para>
/// </summary>
public sealed class ListServiceCategoriesQueryHandler
    : IQueryHandler<ListServiceCategoriesQuery, IReadOnlyList<ServiceCategoryResponse>>
{
    private readonly IServiceCategoryRepository _repository;
    private readonly ServiceCategoryResolver _resolver;
    private readonly ProviderAuthorizationService _authorization;

    public ListServiceCategoriesQueryHandler(
        IServiceCategoryRepository repository,
        ServiceCategoryResolver resolver,
        ProviderAuthorizationService authorization)
    {
        _repository = repository;
        _resolver = resolver;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<ServiceCategoryResponse>>> Handle(
        ListServiceCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var includeRetired = query.IncludeRetired && _authorization.IsStaff;

        var categories = query.ParentId is { } parentId
            ? await _repository.ListChildrenAsync(
                ServiceCategoryId.From(parentId), cancellationToken)
            : includeRetired
                ? await _repository.ListAllAsync(includeRetired: true, cancellationToken)
                : await _resolver.ListAsync(cancellationToken);

        var visible = includeRetired
            ? categories
            : categories.Where(category => category.IsActive).ToList();

        return Result.Success<IReadOnlyList<ServiceCategoryResponse>>(
            visible
                .Select(category => ServiceCategoryMapper.ToResponse(category, query.LanguageCode))
                .ToList());
    }
}
