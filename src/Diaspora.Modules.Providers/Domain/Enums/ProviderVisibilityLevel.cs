namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// Who is allowed to see a provider.
/// <para>
/// Deliberately parallel to B6's <c>VisibilityLevel</c> in shape but not shared with it, and
/// the reason is worth stating: B6's levels describe the audience of a <em>post</em>, which
/// includes <c>Followers</c> and <c>Mentioned</c> — neither of which means anything for a
/// provider. Reusing that enum would have put two members into every provider audience
/// selector that can never be chosen, and would have coupled B7's frozen contract to changes
/// B6 makes for reasons that have nothing to do with providers.
/// </para>
/// <para>
/// The scoping values (<see cref="CountryCommunity"/>, <see cref="AreaOnly"/>) carry their
/// country or area on <see cref="ValueObjects.ProviderVisibility"/> rather than in the enum,
/// so a new scope is a new field rather than a combinatorial explosion of members.
/// </para>
/// </summary>
public enum ProviderVisibilityLevel
{
    /// <summary>Anyone, signed in or not. The default and the overwhelming majority.</summary>
    Public = 1,

    /// <summary>Any signed-in member of the platform.</summary>
    Members = 2,

    /// <summary>
    /// Members of one country community. A Ghanaian caterer who only wants to be found by the
    /// Ghanaian community, which is a real and frequently expressed preference.
    /// </summary>
    CountryCommunity = 3,

    /// <summary>
    /// Members near the provider. Since 14 Oct 2026 no catalogue area is stored: "near" is the
    /// provider's own pin and service areas (country, town, radius). Discovery admits it for any
    /// signed-in member, as it always has; nothing here narrows by a catalogue node any more.
    /// </summary>
    AreaOnly = 4,

    /// <summary>
    /// Reachable by direct link but excluded from discovery and search. What a provider chooses
    /// while it fixes its photographs.
    /// </summary>
    Unlisted = 5,

    /// <summary>Visible only to the provider's own owners, managers and editors.</summary>
    Private = 6
}
