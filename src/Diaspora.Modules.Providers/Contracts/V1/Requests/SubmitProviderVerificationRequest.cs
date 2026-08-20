namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Asks for a provider to be verified.
/// <para>
/// <b>Asks. It does not grant.</b> The response says a claim was submitted, never that it was
/// approved — the outcome arrives later, from Trust, through an integration event. ADR-002's
/// separation applied to the most consequential badge this platform renders.
/// </para>
/// </summary>
public sealed record SubmitProviderVerificationRequest
{
    /// <summary>The <c>ProviderVerificationKind</c> name — <c>Business</c>, <c>Organization</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Required for a <c>Certification</c> claim; ignored otherwise.</summary>
    public Guid? CertificationId { get; init; }

    /// <summary>Media identifiers for the evidence. References, never URLs.</summary>
    public IReadOnlyList<Guid> EvidenceMediaIds { get; init; } = [];
}
