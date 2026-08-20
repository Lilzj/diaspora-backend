namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What a post is about.
/// <para>
/// All fourteen members are the client vocabulary verbatim, including the two-word MarketPrices
/// which the client spells in PascalCase and labels with a space. Renaming any of them breaks
/// the icon and label maps the client already ships, which are keyed by exactly these literals.
/// </para>
/// <para>
/// The category is not decoration: it selects the default expiry. A traffic post that outlives
/// the traffic is worse than no post, and the client already encodes a per-category default
/// which B6 reproduces server-side so the two cannot drift.
/// </para>
/// </summary>
public enum PostCategory
{
    Traffic = 0,

    Police = 1,

    Immigration = 2,

    Weather = 3,

    Education = 4,

    Healthcare = 5,

    Shopping = 6,

    MarketPrices = 7,

    Employment = 8,

    Emergency = 9,

    Community = 10,

    Transportation = 11,

    Utilities = 12,

    Other = 13
}
