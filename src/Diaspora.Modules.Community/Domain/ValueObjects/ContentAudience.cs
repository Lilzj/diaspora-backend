using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// The audience a piece of content declares for itself.
/// <para>
/// A declaration, not a decision. The audience says what the author asked for; whether a
/// particular member may actually read the content is answered by the visibility evaluators at
/// read time, because the answer depends on the reader — their verification, their location,
/// whether they follow the author, whether either has blocked the other — and none of that is
/// knowable when the post is written.
/// </para>
/// <para>
/// Keeping the two apart is what lets B6 consume Identity, Trust and Location for visibility
/// without duplicating any of them: this type stores the author's intent, and the evaluators
/// ask the owning modules for everything else.
/// </para>
/// </summary>
public sealed class ContentAudience : ValueObject
{
    private ContentAudience(
        VisibilityLevel level,
        string? countryCode,
        Guid? areaId,
        VisibilityRuleId? ruleId)
    {
        Level = level;
        CountryCode = countryCode;
        AreaId = areaId;
        RuleId = ruleId;
    }

    private ContentAudience() { }

    public VisibilityLevel Level { get; private set; }

    /// <summary>Set when the level is Country. ISO 3166-1 alpha-2, upper-cased.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>Set when the level is Neighborhood. A Location administrative area.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>Set when the level is Custom. Names a stored visibility rule.</summary>
    public VisibilityRuleId? RuleId { get; private set; }

    public bool IsPublic => Level == VisibilityLevel.Public;

    public bool IsPrivate => Level == VisibilityLevel.Private;

    // A fresh instance per call, not a cached one. These map as EF owned types, and an owned
    // instance shared between two aggregates in one DbContext is tracked against whichever owner
    // EF saw first — the second silently writes nulls into its NOT NULL columns and the save
    // fails with a constraint violation naming a column nothing went near. It only bites when
    // something adds two posts in one unit of work, which no request does and every bulk import,
    // seeder and test fixture will.
    public static ContentAudience Public => new(VisibilityLevel.Public, null, null, null);

    public static ContentAudience Private => new(VisibilityLevel.Private, null, null, null);

    public static ContentAudience Members => new(VisibilityLevel.Members, null, null, null);

    public static ContentAudience Verified =>
        new(VisibilityLevel.VerifiedMembers, null, null, null);

    public static ContentAudience Followers =>
        new(VisibilityLevel.Followers, null, null, null);

    public static ContentAudience ForCountry(string countryCode) =>
        new(VisibilityLevel.Country, countryCode?.ToUpperInvariant(), null, null);

    public static ContentAudience ForNeighborhood(Guid areaId) =>
        new(VisibilityLevel.Neighborhood, null, areaId, null);

    public static ContentAudience ForRule(VisibilityRuleId ruleId) =>
        new(VisibilityLevel.Custom, null, null, ruleId);

    /// <summary>
    /// The audience a level implies when the caller supplies no qualifier. Used when a request
    /// names a level as a bare string, which is how the wire contract expresses the common case.
    /// </summary>
    public static ContentAudience FromLevel(
        VisibilityLevel level, string? countryCode = null, Guid? areaId = null) => level switch
        {
            VisibilityLevel.Country when countryCode is not null => ForCountry(countryCode),
            VisibilityLevel.Neighborhood when areaId is not null => ForNeighborhood(areaId.Value),
            _ => new ContentAudience(level, countryCode?.ToUpperInvariant(), areaId, null)
        };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Level;
        yield return CountryCode;
        yield return AreaId;
        yield return RuleId;
    }
}
