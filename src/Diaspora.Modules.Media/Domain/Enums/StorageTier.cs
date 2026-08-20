namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// How eagerly the bytes need to be available.
/// <para>
/// Recorded on the asset rather than inferred at read time, because moving an object between
/// tiers is a provider operation with a cost and a latency, and the domain has to know which
/// tier an object is actually in before it promises anyone a millisecond read.
/// </para>
/// </summary>
public enum StorageTier
{
    /// <summary>Immediately readable. Everything a member can see in the app.</summary>
    Hot = 0,

    /// <summary>Readable with a small latency penalty. Older listings, closed conversations.</summary>
    Cool = 1,

    /// <summary>Retrieval takes minutes to hours. Retention copies of reviewed documents.</summary>
    Archive = 2
}
