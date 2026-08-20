namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// Changes an asset's own description and audience.
/// <para>
/// Notably absent: anything about where the bytes are. Storage is not a property a caller may
/// change, and exposing one would be the first crack in the abstraction.
/// </para>
/// </summary>
public sealed record UpdateMediaRequest
{
    /// <summary>
    /// Accessible description.
    /// <para>
    /// Worth having a field for even though no current screen collects one: thirty-one media
    /// fields across the client and not a single alt text, which is a marketplace a
    /// screen-reader user cannot shop.
    /// </para>
    /// </summary>
    public string? AltText { get; init; }

    /// <summary>The client's <c>ImageAttachment.caption</c>.</summary>
    public string? Caption { get; init; }

    /// <summary>
    /// <c>private</c>, <c>authenticated</c> or <c>public</c>. Omit to leave unchanged.
    /// <para>
    /// A verification document cannot be made public at any tier, by any caller. That refusal
    /// is in the domain, not here.
    /// </para>
    /// </summary>
    public string? Visibility { get; init; }
}
