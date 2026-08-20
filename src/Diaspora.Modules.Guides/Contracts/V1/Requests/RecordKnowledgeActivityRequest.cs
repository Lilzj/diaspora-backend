namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Tells the directory that a reader did something with it.
/// <para>
/// <b>The client reports it, because the server cannot see it.</b> Opening a country page is a
/// request the server observes; tapping through to a guide from a shelf, or reaching the end of one,
/// is not — the guide read is a request to a different endpoint that carries no memory of where the
/// reader came from. Without this, the directory could count arrivals and never learn whether any of
/// them led anywhere, which is the one number that says whether it works.
/// </para>
/// <para>
/// <b>Nothing here identifies a reader, and nothing accepts an identity if one is offered.</b> The
/// counters this feeds are aggregate totals per country and per category. A directory that recorded
/// which member read about immigration would be holding something it has no business holding.
/// </para>
/// </summary>
public sealed record RecordKnowledgeActivityRequest
{
    /// <summary>The navigation row involved, for a category view or a discovery from one.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>The guide opened or completed.</summary>
    public Guid? GuideId { get; init; }

    /// <summary>The learning path started or completed.</summary>
    public Guid? LearningPathId { get; init; }

    /// <summary>The reader's locale, when the client has one. Never an identity.</summary>
    public string? Locale { get; init; }
}
