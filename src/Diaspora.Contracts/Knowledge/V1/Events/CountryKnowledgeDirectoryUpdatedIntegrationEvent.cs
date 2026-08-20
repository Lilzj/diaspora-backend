using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Knowledge.V1.Events;

/// <summary>
/// One country's knowledge directory changed shape.
/// <para>
/// <b>The cache-invalidation event, and it is deliberately about the directory rather than about
/// what changed inside it.</b> A consumer holding a rendered country page — a CDN edge, a mobile
/// client's offline shelf, a future recommendation model — needs to know the page is stale. It
/// does not need to replay the individual reorderings that made it so, and an event per entry
/// would turn the common editorial session, dragging nine categories into a new order, into nine
/// messages that all mean the same thing.
/// </para>
/// <para>
/// <see cref="Change"/> names what happened in one word — <c>created</c>, <c>metadata</c>,
/// <c>arranged</c>, <c>published</c>, <c>archived</c> — because the two that matter to a consumer
/// are distinguishable: a republished directory is newly visible, an arranged one merely looks
/// different.
/// </para>
/// </summary>
public sealed record CountryKnowledgeDirectoryUpdatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Upper-case ISO 3166-1 alpha-2. The directory's identity to every consumer.</summary>
    public required string CountryCode { get; init; }

    /// <summary>One of <c>created</c>, <c>metadata</c>, <c>arranged</c>, <c>published</c>, <c>archived</c>.</summary>
    public required string Change { get; init; }

    /// <summary>Whether the directory is visible to readers after this change.</summary>
    public required bool IsPublished { get; init; }

    /// <summary>How many categories the directory now lists. A consumer renders a skeleton from it.</summary>
    public required int CategoryCount { get; init; }

    /// <summary>The staff member who made the change, when there was one.</summary>
    public Guid? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
