namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// How a shelf gets onto a knowledge category page.
/// <para>
/// <b>The seam that makes the brief's section list a default rather than a ceiling.</b> B8.1 ships
/// featured, most helpful, recently updated, official, community and recommended-next. A
/// deployment that wants "Guides your neighbours found useful", or a future module that wants
/// "Answers to questions asked this week", ships a class implementing this and the shelf appears —
/// on every category, on every country, in the position it asks for, with no change to any query
/// handler, response contract or client.
/// </para>
/// <para>
/// <b>A provider decides whether it applies.</b> <see cref="AppliesTo"/> defaults to yes, and the
/// interesting implementations say no: a recommended-next shelf is meaningless without a curated
/// list, and an official shelf is noise in a country where nothing has been verified yet. Returning
/// no content is also a valid answer — the composer drops empty sections rather than rendering a
/// heading over nothing.
/// </para>
/// <para>
/// <b>Providers are scoped, and must be.</b> Nearly every one holds a repository. B4 recorded a
/// singleton registry over scoped implementations as the captive dependency that stops the host
/// booting; B6, B7 and B8 each repeated the note. This registry pays one dictionary construction
/// per request rather than repeating it a fifth time.
/// </para>
/// </summary>
public interface IKnowledgeSectionProvider
{
    /// <summary>
    /// The section's stable wire key. Unique across providers — a second provider claiming
    /// <c>featured</c> replaces the first rather than adding a duplicate shelf.
    /// </summary>
    string Key { get; }

    /// <summary>Where the shelf sits on the page. Lower renders first.</summary>
    int Order => 100;

    /// <summary>Whether this shelf makes sense for this request at all. Defaults to yes.</summary>
    bool AppliesTo(KnowledgeSectionRequest request) => true;

    /// <summary>
    /// Fills the shelf, or returns null to omit it entirely.
    /// <para>
    /// Null and empty mean the same thing to the composer. Both are offered because they read
    /// differently at the call site: null is "not for this page", empty is "for this page, and
    /// there is nothing yet".
    /// </para>
    /// </summary>
    Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default);
}
