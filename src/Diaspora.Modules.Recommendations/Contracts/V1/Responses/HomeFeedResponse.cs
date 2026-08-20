namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// The home feed.
/// <para>
/// <b><see cref="IsPersonalized"/> is the field the client should surface to the member.</b> A feed
/// that is not personalised because they opted out looks exactly like one that is, and a product
/// that cannot tell somebody which they are looking at is a product asking them to take
/// personalization on trust.
/// </para>
/// <para>
/// <see cref="ProfileCompleteness"/> is here for the same reason: it is what lets the client say
/// "tell us where you live and this gets better", which is the honest prompt and considerably more
/// useful than an empty state.
/// </para>
/// </summary>
public sealed record HomeFeedResponse
{
    public required IReadOnlyList<FeedSectionResponse> Sections { get; init; }

    /// <summary>False when the member turned personalization off. See the class summary.</summary>
    public required bool IsPersonalized { get; init; }

    /// <summary>True while the platform has too little to go on.</summary>
    public required bool IsColdStart { get; init; }

    /// <summary>0–100. What the client can prompt against — see the class summary.</summary>
    public required int ProfileCompleteness { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
