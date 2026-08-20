using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Catalog;

/// <summary>The category catalogue, in the order a settings screen should render it.</summary>
public sealed class ListNotificationCategoriesQueryHandler
    : IQueryHandler<ListNotificationCategoriesQuery, IReadOnlyList<NotificationCategoryResponse>>
{
    private readonly INotificationCategoryRepository _categories;

    public ListNotificationCategoriesQueryHandler(INotificationCategoryRepository categories) =>
        _categories = categories;

    public async Task<Result<IReadOnlyList<NotificationCategoryResponse>>> Handle(
        ListNotificationCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListActiveAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NotificationCategoryResponse>>(
            categories.Select(PreferenceMapper.ToResponse).ToList());
    }
}
