namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One shelf on a knowledge category page — "Most helpful", "Recently updated", "Official".
/// <para>
/// <see cref="Key"/> is the stable identifier a client switches on for an icon or an anchor;
/// <see cref="Title"/> is prose that may be localized and may change. A client that switched on the
/// title would break the first time somebody improved the wording, which is why both are here.
/// </para>
/// <para>
/// A section with no guides is never returned. A heading over an empty list tells a reader the
/// platform has nothing for them in a way that a missing heading does not.
/// </para>
/// </summary>
public sealed record KnowledgeSectionResponse
{
    /// <summary>The stable section key — <c>featured</c>, <c>most-helpful</c>, <c>official</c>.</summary>
    public required string Key { get; init; }

    public required string Title { get; init; }

    /// <summary>Where the shelf sits on the page. Lower renders first.</summary>
    public required int Order { get; init; }

    public IReadOnlyList<GuideCardResponse> Guides { get; init; } = [];
}
