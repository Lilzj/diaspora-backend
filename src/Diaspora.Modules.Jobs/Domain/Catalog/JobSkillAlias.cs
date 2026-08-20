namespace Diaspora.Modules.Jobs.Domain.Catalog;

/// <summary>
/// An alternative spelling that resolves to a catalogued skill.
/// <para>
/// Already normalised through the same function both sides of a match use, so a lookup is an
/// equality comparison on an indexed column rather than a fuzzy match at query time.
/// </para>
/// </summary>
public sealed class JobSkillAlias
{
    private JobSkillAlias(string aliasKey) => AliasKey = aliasKey;

    private JobSkillAlias() { }

    /// <summary>The normalised alternative. Unique across the catalogue.</summary>
    public string AliasKey { get; private set; } = string.Empty;

    internal static JobSkillAlias Create(string aliasKey) => new(aliasKey);
}
