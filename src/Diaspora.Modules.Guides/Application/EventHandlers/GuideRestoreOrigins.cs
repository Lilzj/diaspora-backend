namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Where a restored guide came back from. The <c>RestoredFrom</c> vocabulary, pinned.
/// <para>
/// Two restore paths share one published contract, and a consumer branches on this string — so it
/// is a constant rather than a literal repeated in two relay methods that could drift apart. The
/// distinction is load-bearing: a guide returning from the archive was readable to its author
/// throughout and only needs re-ranking, whereas one returning from deletion was gone from every
/// index and has to be re-added.
/// </para>
/// </summary>
public static class GuideRestoreOrigins
{
    /// <summary>Un-archived. The guide was readable to its author throughout.</summary>
    public const string Archive = "Archive";

    /// <summary>Un-deleted. The guide was gone from every index and must be re-added.</summary>
    public const string Deletion = "Deletion";
}
