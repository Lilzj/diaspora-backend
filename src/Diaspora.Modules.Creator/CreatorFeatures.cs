namespace Diaspora.Modules.Creator;

/// <summary>
/// The module's feature gates.
/// <para>
/// Six rather than one, so a deployment launching the creator programme without a grant fund can
/// turn <see cref="Grants"/> off and leave the rest on. One gate for the whole module would make
/// each of those an all-or-nothing decision, and the platform has paid for that shape before.
/// </para>
/// <para>
/// Names are <c>module.capability</c>, lower case and dotted — DSP5001.
/// </para>
/// </summary>
public static class CreatorFeatures
{
    private const string Prefix = "creator";

    /// <summary>The creator projection, its status ladder and the programme verification.</summary>
    public const string Programme = Prefix + ".programme";

    /// <summary>Milestones and challenges.</summary>
    public const string Challenges = Prefix + ".challenges";

    /// <summary>Community grants.</summary>
    public const string Grants = Prefix + ".grants";

    /// <summary>The Creator Academy.</summary>
    public const string Academy = Prefix + ".academy";

    /// <summary>Referrals and the sign-up bonus.</summary>
    public const string Referrals = Prefix + ".referrals";

    /// <summary>The reward rules catalogue and redemptions.</summary>
    public const string Rewards = Prefix + ".rewards";

    /// <summary>The public creator directory and the leaderboards.</summary>
    public const string Discovery = Prefix + ".discovery";
}
