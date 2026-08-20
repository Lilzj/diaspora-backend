namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>
/// A report.
/// <para>
/// Accepts both taxonomies the client has. Its moderation types declare eight PascalCase reason
/// codes; its report modal submits five lower-case category keys. Both are parsed, because the
/// modal is what actually renders and the codes are what the queue needs.
/// </para>
/// </summary>
public sealed record CreateReportRequest
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    /// <summary>
    /// A <c>ReportReasonCode</c> name, or one of the modal keys: <c>conflicting</c>,
    /// <c>inaccurate</c>, <c>spam</c>, <c>abusive</c>, <c>other</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>The modal calls this <c>reason</c>. Supporting detail, not the code.</summary>
    public string? Description { get; init; }

    /// <summary>The modal's <c>conflictingLink</c>. Stored as text; never fetched.</summary>
    public string? EvidenceUrl { get; init; }
}
