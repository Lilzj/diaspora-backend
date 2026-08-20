using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// Who may see a provider, and within what scope.
/// <para>
/// The level and its scope travel together because they are meaningless apart:
/// <see cref="ProviderVisibilityLevel.CountryCommunity"/> without a country code is a rule that
/// admits nobody, and a country code without that level is a field nothing reads. Making them
/// one value object means the invariant is enforced at construction rather than checked at each
/// of the seven places visibility is evaluated.
/// </para>
/// <para>
/// Evaluation itself is <em>not</em> here. A value object cannot know who the viewer is, and
/// putting the decision on the data would mean every caller passing a viewer into a property
/// getter. The decision lives in <c>ProviderVisibilityEvaluator</c>, which is also where the
/// contributor seam hangs.
/// </para>
/// </summary>
public sealed class ProviderVisibility : ValueObject
{
    private ProviderVisibility(ProviderVisibilityLevel level, string? countryCode, Guid? areaId)
    {
        Level = level;
        CountryCode = countryCode;
        AreaId = areaId;
    }

    private ProviderVisibility() { }

    public ProviderVisibilityLevel Level { get; private set; } = ProviderVisibilityLevel.Public;

    /// <summary>Set only when <see cref="Level"/> is <c>CountryCommunity</c>.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>Set only when <see cref="Level"/> is <c>AreaOnly</c>.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>Whether this provider may appear in discovery, search and nearby results.</summary>
    public bool IsDiscoverable =>
        Level is not (ProviderVisibilityLevel.Unlisted or ProviderVisibilityLevel.Private);

    /// <summary>Whether an anonymous caller may read it.</summary>
    public bool IsPublic => Level == ProviderVisibilityLevel.Public;

    public static ProviderVisibility Public =>
        new(ProviderVisibilityLevel.Public, null, null);

    public static ProviderVisibility Members =>
        new(ProviderVisibilityLevel.Members, null, null);

    public static ProviderVisibility Unlisted =>
        new(ProviderVisibilityLevel.Unlisted, null, null);

    public static ProviderVisibility Private =>
        new(ProviderVisibilityLevel.Private, null, null);

    /// <summary>
    /// Builds a visibility, discarding scope that the level does not use.
    /// <para>
    /// Discarding rather than refusing, because a client that switches a provider from
    /// country-scoped back to public will send the stale country code with it, and failing that
    /// request would make the audience selector unusable for a reason no user could diagnose.
    /// </para>
    /// </summary>
    public static ProviderVisibility Create(
        ProviderVisibilityLevel level, string? countryCode = null, Guid? areaId = null) =>
        level switch
        {
            ProviderVisibilityLevel.CountryCommunity when !string.IsNullOrWhiteSpace(countryCode) =>
                new ProviderVisibility(level, countryCode.Trim().ToUpperInvariant(), null),

            // A country scope with no country is not a narrower audience, it is an unenforceable
            // one. Widening to Members is the safe reading: still signed-in only, still visible.
            ProviderVisibilityLevel.CountryCommunity => Members,

            ProviderVisibilityLevel.AreaOnly when areaId is { } area && area != Guid.Empty =>
                new ProviderVisibility(level, null, area),

            ProviderVisibilityLevel.AreaOnly => Members,

            _ => new ProviderVisibility(level, null, null)
        };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Level;
        yield return CountryCode;
        yield return AreaId;
    }
}
