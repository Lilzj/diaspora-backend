namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// How loudly a post should be surfaced.
/// <para>
/// Member names taken verbatim from the client, where they appear as a type in its update
/// models and as four buttons in its composer. They drive three separate behaviours: the
/// urgent banner on the home feed, the default expiry, and the confirmation threshold a post
/// must reach before the community considers it verified.
/// </para>
/// </summary>
public enum UrgencyLevel
{
    Low = 0,

    Normal = 1,

    Urgent = 2,

    /// <summary>
    /// Reserved for genuine safety information. Costs the most to get wrong, which is why
    /// posting opens at Verified and why the emergency confirmation threshold is the highest.
    /// </summary>
    Emergency = 3
}
