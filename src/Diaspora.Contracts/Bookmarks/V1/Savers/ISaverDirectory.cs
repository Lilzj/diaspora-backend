namespace Diaspora.Contracts.Bookmarks.V1.Savers;

/// <summary>
/// Who saved one thing — 21 Oct 2026.
/// <para>
/// <c>guide.savedChanged</c> promised "a guide you saved was updated" and fired for every member's
/// automation on every guide edit, drafts included, because the module that consumes the edit could
/// not ask who had saved the guide. Bookmarks owns that answer and composes before every module that
/// needs it, so it answers through this seam rather than being referenced.
/// </para>
/// <para>
/// <b>Bounded, and most recent first.</b> A widely saved guide must not turn one edit into an
/// unbounded read or an unbounded owner list for the engine; the caller states its limit and gets the
/// most recent savers up to it. With no implementation registered, a caller treats the answer as
/// nobody — never as everybody.
/// </para>
/// </summary>
public interface ISaverDirectory
{
    /// <summary>
    /// The members who saved <paramref name="targetId"/> under <paramref name="targetType"/> (a
    /// <see cref="SavedTargetTypes"/> key), most recently saved first, at most
    /// <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListSaversAsync(
        string targetType, Guid targetId, int limit, CancellationToken cancellationToken = default);
}

/// <summary>The bookmark target keys a consumer of <see cref="ISaverDirectory"/> asks about.</summary>
public static class SavedTargetTypes
{
    /// <summary>A guide, saved by its <c>GuideId</c>.</summary>
    public const string Guide = "guide";
}
