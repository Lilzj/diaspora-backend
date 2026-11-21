namespace Diaspora.Contracts.Financial.V1.Advertising;

/// <summary>
/// Who a campaign is aimed at, as a dimension name.
/// <para>
/// <b>This enum is the honest answer to "which dimensions does the delivery engine actually
/// honour".</b> The register asks for that statement by name, because the client's builder collects
/// seven dimensions and the wire carried one. Every member here is a dimension the campaign stores
/// <em>and</em> the estimate reads; a dimension the engine cannot honour does not get a member.
/// </para>
/// </summary>
public enum AdTargetingDimension
{
    /// <summary>ISO 3166-1 alpha-2. The one dimension that existed before this phase.</summary>
    Country = 1,

    /// <summary>A community id, from B18's directory.</summary>
    Community = 2,

    /// <summary>Legacy: a catalogue neighbourhood id. Not stored since 14 Oct 2026; see <see cref="Town"/>.</summary>
    Neighbourhood = 3,

    /// <summary>An interest key.</summary>
    Interest = 4,

    /// <summary>A vertical's category key — a job category, a marketplace category.</summary>
    Category = 5,

    /// <summary>A town by name (14 Oct 2026), replacing catalogue neighbourhoods.</summary>
    Town = 6,
}
