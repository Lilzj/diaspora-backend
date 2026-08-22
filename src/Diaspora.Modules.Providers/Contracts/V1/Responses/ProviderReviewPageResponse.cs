namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>One page of reviews, cursor-paged for the same reason providers are.</summary>
public sealed record ProviderReviewPageResponse
{
    public required IReadOnlyList<ProviderReviewResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>The rollup, so a client rendering the first page needs no second call.</summary>
    public ProviderReviewSummaryResponse? Summary { get; init; }

    /// <summary>
    /// Which provider this page belongs to. Null when the caller runs none.
    /// <para>
    /// Added for <c>GET /providers/me/reviews</c>, and it is what lets a screen tell *"you have no
    /// reviews yet"* from *"you are not a provider"* — two different sentences that an empty
    /// <see cref="Items"/> list cannot distinguish, and only one of which is about reviews. It is
    /// populated on the per-provider route too, because a client that has to remember which id it
    /// asked for is a client that will one day render the wrong heading.
    /// </para>
    /// </summary>
    public Guid? ProviderId { get; init; }

    /// <summary>
    /// The page for a caller who runs no provider.
    /// <para>
    /// Not a 404. Having no provider is an ordinary state, and answering "not found" would make a
    /// screen render an error for a member who has simply never registered a business.
    /// </para>
    /// </summary>
    public static ProviderReviewPageResponse Empty() => new()
    {
        Items = [],
        HasMore = false,
        NextCursor = null,
        Summary = null,
        ProviderId = null
    };
}
