namespace Diaspora.Modules.Guides.Extensibility.Verification;

/// <summary>
/// One piece of evidence: a Media reference and a claim about what it is.
/// <para>
/// The type travels with the reference because the deciding system's document rules do the
/// refusing — evidence that cannot describe itself is refused before a reviewer ever sees it,
/// which is the B3.0.2 trap this record exists to avoid.
/// </para>
/// </summary>
public sealed record GuideVerificationEvidence
{
    /// <summary>The Media asset. A reference, never a URL and never bytes (ADR-005).</summary>
    public required Guid MediaId { get; init; }

    /// <summary>
    /// The document type in the deciding system's vocabulary. Null lets the shipped provider
    /// supply the guide evidence type Trust's handler accepts.
    /// </summary>
    public string? DocumentType { get; init; }
}
