namespace Diaspora.Modules.Marketplace.Domain.Policies;

/// <summary>
/// Widths for the fields this module stores on somebody else's behalf.
/// <para>
/// Separate from <see cref="MarketplaceLimits"/> because these are not the module's rules: a Trust
/// badge is as long as Trust makes it and a Media content type is as long as the MIME registry
/// allows. They live in the domain rather than in the persistence layer because the value objects
/// truncate against them at construction — a badge that arrives longer than the column is a
/// rendering problem, and refusing a Trust outcome over it would be a verification lost.
/// </para>
/// </summary>
public static class MarketplacePersistenceLimits
{
    /// <summary>Trust's badge label, projected. Rendered, never interpreted.</summary>
    public const int Badge = 100;

    /// <summary>The reason on a trust mark or a moderation decision.</summary>
    public const int Reason = 500;

    /// <summary>A MIME type as Media reported it.</summary>
    public const int ContentType = 200;

    /// <summary>An external conversation handle, as a messaging provider issued it.</summary>
    public const int ExternalReference = 200;
}
