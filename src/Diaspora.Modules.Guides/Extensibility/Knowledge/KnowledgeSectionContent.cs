namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// What a section provider returns: a labelled, ordered list of guide identifiers.
/// <para>
/// <b>Identifiers, not cards, and that is the N+1 guard.</b> Seven providers each rendering their
/// own cards would be seven separate resolutions of overlapping guides on one screen. Returning
/// identifiers lets the composer collect every one across every section, resolve them in a single
/// batched read, and hand each section the cards it asked for — which is also why a guide featured
/// in two sections costs one lookup rather than two.
/// </para>
/// <para>
/// A provider returning an empty list is normal and means the shelf is empty for this country: a
/// section with no guides is simply not rendered, which is far better than rendering a heading over
/// nothing.
/// </para>
/// </summary>
public sealed record KnowledgeSectionContent
{
    /// <summary>The section's wire key — <c>featured</c>, <c>most-helpful</c>. Clients switch on it.</summary>
    public required string Key { get; init; }

    /// <summary>The heading. Localized by the provider if it can be; plain otherwise.</summary>
    public required string Title { get; init; }

    /// <summary>The guides, in the order the section wants them rendered.</summary>
    public IReadOnlyList<Guid> GuideIds { get; init; } = [];

    /// <summary>Where the shelf sits on the page. Lower renders first.</summary>
    public int Order { get; init; }
}
