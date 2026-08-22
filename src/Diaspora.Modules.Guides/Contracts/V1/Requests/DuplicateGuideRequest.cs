namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// What a caller may say about the copy — BG-07.
/// <para>
/// One optional field, and the body itself is optional. Everything else about a duplicate is taken
/// from the source or reset by the handler, and letting a caller choose per-field what to carry over
/// would turn a copy into a create with extra steps.
/// </para>
/// </summary>
public sealed record DuplicateGuideRequest
{
    /// <summary>
    /// A title for the copy. Null takes the source's, prefixed <c>Copy of </c> — because two
    /// identically named rows in the author's own drafts list is the first thing that makes a
    /// duplicate feature annoying to use.
    /// </summary>
    public string? Title { get; init; }
}
