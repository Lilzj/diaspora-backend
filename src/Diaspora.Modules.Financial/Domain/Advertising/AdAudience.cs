using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// Who a campaign is aimed at.
/// <para>
/// <b>The register's complaint was not that targeting was wrong; it was that the builder collected
/// seven dimensions and the wire carried one.</b> "A builder that collected six more and sent one
/// would be collecting five for nothing" — so this type is the answer to the other half of that
/// entry, which asks for "a statement of which dimensions the delivery engine actually honours".
/// Every field here is honoured. There is no field for a dimension that is collected and dropped.
/// </para>
/// <para>
/// <b>Empty means everyone, on every dimension independently.</b> A campaign with no countries and
/// three interests reaches those interests anywhere; one with two countries and no interests reaches
/// everyone in those countries. That is a union within a dimension and an intersection across them,
/// and it is the only reading that makes an unset field mean "unset" rather than "nobody".
/// </para>
/// <para>
/// <b>No demographic dimension exists, deliberately.</b> Age, gender and inferred ethnicity are not
/// here and are not planned: §55 forbids inferring identity characteristics, and a diaspora platform
/// that let an advertiser target inferred nationality would be building the thing the whole
/// community model exists to avoid. Country of *presence* is a place somebody told the platform they
/// live; it is not a claim about who they are.
/// </para>
/// </summary>
public sealed class AdAudience : ValueObject
{
    /// <summary>The cap on any one dimension. A selection longer than this is a filter that filters nothing.</summary>
    public const int MaximumPerDimension = 50;

    private readonly List<string> _countryCodes = [];
    private readonly List<Guid> _communityIds = [];
    private readonly List<Guid> _neighbourhoodIds = [];
    private readonly List<string> _interests = [];
    private readonly List<string> _categories = [];

    private AdAudience() { }

    /// <summary>ISO 3166-1 alpha-2, upper case. Where the campaign is shown.</summary>
    public IReadOnlyCollection<string> CountryCodes => _countryCodes.AsReadOnly();

    /// <summary>Communities from B18's directory.</summary>
    public IReadOnlyCollection<Guid> CommunityIds => _communityIds.AsReadOnly();

    public IReadOnlyCollection<Guid> NeighbourhoodIds => _neighbourhoodIds.AsReadOnly();

    public IReadOnlyCollection<string> Interests => _interests.AsReadOnly();

    /// <summary>A vertical's own category keys — a job category, a marketplace category.</summary>
    public IReadOnlyCollection<string> Categories => _categories.AsReadOnly();

    /// <summary>Everyone. What a campaign that named no audience is aimed at.</summary>
    public static AdAudience Everyone() => new();

    /// <summary>
    /// Reads a selection.
    /// <para>
    /// <b>Nothing here throws and nothing is refused.</b> Duplicates collapse, blanks disappear,
    /// country codes upper-case, and a dimension longer than the cap is truncated. An advertiser
    /// pasting a list with a trailing empty line should get their campaign, not a validation error —
    /// and every one of these normalisations changes what is *stored*, so the campaign the estimate
    /// reads and the campaign the delivery engine reads are the same one.
    /// </para>
    /// </summary>
    public static AdAudience Create(
        IEnumerable<string>? countryCodes = null,
        IEnumerable<Guid>? communityIds = null,
        IEnumerable<Guid>? neighbourhoodIds = null,
        IEnumerable<string>? interests = null,
        IEnumerable<string>? categories = null)
    {
        var audience = new AdAudience();

        audience._countryCodes.AddRange(Codes(countryCodes));
        audience._communityIds.AddRange(Identifiers(communityIds));
        audience._neighbourhoodIds.AddRange(Identifiers(neighbourhoodIds));
        audience._interests.AddRange(Keys(interests));
        audience._categories.AddRange(Keys(categories));

        return audience;
    }

    /// <summary>Whether this narrows anything at all.</summary>
    public bool IsEveryone =>
        _countryCodes.Count == 0
        && _communityIds.Count == 0
        && _neighbourhoodIds.Count == 0
        && _interests.Count == 0
        && _categories.Count == 0;

    /// <summary>
    /// The dimensions this audience actually constrains.
    /// <para>
    /// Read by the estimate and by the campaign response, so a client can show "narrowed by country
    /// and community" without inspecting five lists and guessing which of them counted.
    /// </para>
    /// </summary>
    public IReadOnlyList<AdTargetingDimension> ConstrainedDimensions()
    {
        var dimensions = new List<AdTargetingDimension>(5);

        if (_countryCodes.Count > 0) dimensions.Add(AdTargetingDimension.Country);
        if (_communityIds.Count > 0) dimensions.Add(AdTargetingDimension.Community);
        if (_neighbourhoodIds.Count > 0) dimensions.Add(AdTargetingDimension.Neighbourhood);
        if (_interests.Count > 0) dimensions.Add(AdTargetingDimension.Interest);
        if (_categories.Count > 0) dimensions.Add(AdTargetingDimension.Category);

        return dimensions;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordered, because the lists are normalised on the way in and two audiences built from the
        // same selection in a different order are the same audience.
        yield return string.Join(',', _countryCodes.Order(StringComparer.Ordinal));
        yield return string.Join(',', _communityIds.Order());
        yield return string.Join(',', _neighbourhoodIds.Order());
        yield return string.Join(',', _interests.Order(StringComparer.Ordinal));
        yield return string.Join(',', _categories.Order(StringComparer.Ordinal));
    }

    private static IEnumerable<string> Codes(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(value => value?.Trim().ToUpperInvariant())
            .Where(value => value is { Length: 2 })
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumPerDimension);

    private static IEnumerable<string> Keys(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumPerDimension);

    private static IEnumerable<Guid> Identifiers(IEnumerable<Guid>? values) =>
        (values ?? [])
            .Where(value => value != Guid.Empty)
            .Distinct()
            .Take(MaximumPerDimension);
}
