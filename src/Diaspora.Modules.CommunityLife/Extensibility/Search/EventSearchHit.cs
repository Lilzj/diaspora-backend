namespace Diaspora.Modules.CommunityLife.Extensibility.Search;

/// <summary>
/// One event as a search engine returns it.
/// <para>
/// Identifier and score only, and that is the whole point of the port. An engine returns what
/// matched and how well; the module reads the aggregates. An engine that returned rendered content
/// would have to be kept in step with every field the client renders, which is exactly the coupling
/// that makes search engines impossible to replace.
/// </para>
/// </summary>
public sealed record EventSearchHit(Guid EventId, double Score);
