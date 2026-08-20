namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One thing a notice points at.
/// <para>
/// <b>A kind, an identifier and the editor's label — and nothing the owning module owns.</b> No
/// title, no status, no summary: the client resolves the identifier against whichever module the kind
/// names, with that module's visibility rules and freshness. A notice that shipped a job title here
/// would go on showing it after the job closed.
/// </para>
/// </summary>
public sealed record NoticeReferenceResponse
{
    /// <summary>PascalCase — <c>Guide</c>, <c>Event</c>. What a client switches on to resolve it.</summary>
    public required string Kind { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Why the editor linked it. Their words about this notice, not the target's.</summary>
    public string? Label { get; init; }

    public required int Position { get; init; }
}
