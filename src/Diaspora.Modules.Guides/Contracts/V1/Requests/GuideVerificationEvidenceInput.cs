namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One piece of verification evidence: a Media reference and a claim about what it shows.
/// <para>
/// The document type matters — Trust's verification types accept particular kinds of document
/// and refuse the rest, so evidence that cannot describe itself is evidence the reviewer never
/// sees (the B3.0.2 lesson).
/// </para>
/// </summary>
public sealed record GuideVerificationEvidenceInput
{
    /// <summary>The Media asset. A reference, never a URL and never bytes.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>
    /// Trust's <c>VerificationDocumentType</c> name. Omitted, the module supplies the guide
    /// evidence type the verification handler accepts.
    /// </summary>
    public string? DocumentType { get; init; }
}
