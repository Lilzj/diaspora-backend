using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>One category a member may file an update under.</summary>
public sealed record PostCategoryResponse
{
    /// <summary>What to send as <c>category</c> when creating a post.</summary>
    public required string Key { get; init; }

    public required string Label { get; init; }
}

/// <summary>
/// The update categories, served rather than copied into the app.
/// <para>
/// Added 13 Oct 2026. The app offered six categories of its own, and five of them — safety alert,
/// general, housing tip, job lead, question — are not categories here, so the server filed every
/// one as <c>Other</c> without a word. The owner chose this list over the app's; serving it is
/// what keeps the two from drifting apart again.
/// </para>
/// </summary>
public sealed record GetPostCategoriesQuery : IQuery<IReadOnlyList<PostCategoryResponse>>;

public sealed class GetPostCategoriesQueryHandler
    : IQueryHandler<GetPostCategoriesQuery, IReadOnlyList<PostCategoryResponse>>
{
    private static readonly IReadOnlyDictionary<PostCategory, string> Labels =
        new Dictionary<PostCategory, string>
        {
            [PostCategory.MarketPrices] = "Market prices"
        };

    public Task<Result<IReadOnlyList<PostCategoryResponse>>> Handle(
        GetPostCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<PostCategoryResponse> categories = Enum.GetValues<PostCategory>()
            .Select(category => new PostCategoryResponse
            {
                Key = CommunityWireNames.ToWire(category),
                Label = Labels.GetValueOrDefault(category, category.ToString())
            })
            .ToList();

        return Task.FromResult(Result.Success(categories));
    }
}
