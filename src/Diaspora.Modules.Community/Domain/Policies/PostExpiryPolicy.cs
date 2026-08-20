using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Domain.Policies;

/// <summary>
/// How long a post stays current, by category.
/// <para>
/// <b>These hours already exist on the client</b>, in its <c>DEFAULT_EXPIRATION_HOURS</c> map,
/// and a unit test pins every one of them against the client literal. Its composer offers a
/// member 2, 4, 6, 12, 24 or 48 hours; this table is what happens when they do not choose.
/// </para>
/// <para>
/// Expiry is the mechanism that makes a hyperlocal feed trustworthy rather than merely full. A
/// traffic post that outlives the traffic is worse than no post at all, because it teaches
/// members that the feed is stale and they stop reading the ones that matter. Immigration and
/// employment posts run for a week because the facts do; education runs for a month.
/// </para>
/// <para>
/// An expired post is not deleted. It leaves the feeds, keeps its permalink, and stays
/// readable — the information may be historical but the conversation under it was real.
/// </para>
/// </summary>
public static class PostExpiryPolicy
{
    /// <summary>The ceiling any post may declare, whatever its category. Thirty days.</summary>
    public const int MaxHours = 720;

    /// <summary>The floor. Below an hour a post expires before most members see it.</summary>
    public const int MinHours = 1;

    /// <summary>The default lifetime, in hours, for a category.</summary>
    public static int DefaultHours(PostCategory category) => category switch
    {
        PostCategory.Traffic => 4,
        PostCategory.Police => 6,
        PostCategory.Emergency => 24,
        PostCategory.MarketPrices => 24,
        PostCategory.Weather => 12,
        PostCategory.Immigration => 168,
        PostCategory.Community => 168,
        PostCategory.Education => 720,
        PostCategory.Employment => 720,
        PostCategory.Healthcare => 168,
        PostCategory.Utilities => 12,
        PostCategory.Shopping => 24,
        PostCategory.Transportation => 12,
        _ => 24
    };

    /// <summary>
    /// Clamps a requested lifetime into the permitted band, or falls back to the category
    /// default when nothing was requested.
    /// </summary>
    public static int Resolve(PostCategory category, int? requestedHours) =>
        requestedHours is null
            ? DefaultHours(category)
            : Math.Clamp(requestedHours.Value, MinHours, MaxHours);
}
