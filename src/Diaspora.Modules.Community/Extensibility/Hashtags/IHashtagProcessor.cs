namespace Diaspora.Modules.Community.Extensibility.Hashtags;

/// <summary>
/// Inspects and filters the tags found in a body.
/// <para>
/// A pipeline rather than a single extractor, because the interesting work is not finding a
/// <c>#</c> — it is deciding what to do with what follows. A deployment's blocklist, a
/// synonym map that folds <c>#lagostraffic</c> into <c>#lagos</c>, a language-specific
/// tokeniser, an AI topic tagger that adds tags nobody typed: each is a processor, and none of
/// them requires editing this module.
/// </para>
/// <para>
/// A processor may add, remove or rewrite. Returning an empty list is a legitimate answer and
/// means the body carries no tags.
/// </para>
/// </summary>
public interface IHashtagProcessor
{
    /// <summary>Lower runs first. A blocklist should run late, after any rewriting.</summary>
    int Order => 0;

    IReadOnlyList<HashtagCandidate> Process(
        string body, IReadOnlyList<HashtagCandidate> candidates);
}
