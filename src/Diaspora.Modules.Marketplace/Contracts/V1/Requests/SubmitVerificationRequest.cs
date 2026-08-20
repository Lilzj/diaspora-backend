namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Asks Trust to vouch for a seller or a listing.
/// <para>
/// <b>It carries evidence by reference and says nothing about an outcome.</b> Approval, rejection,
/// revocation and badge assignment are Trust's alone; this module can only ask, and the response
/// it gets back is a case identifier rather than a decision.
/// </para>
/// </summary>
public sealed record SubmitVerificationRequest
{
    /// <summary>
    /// Media identifiers for the supporting documents — a receipt, a registration certificate, a
    /// serial-number photograph. Uploaded through Media beforehand.
    /// </summary>
    public IReadOnlyList<Guid>? EvidenceMediaIds { get; init; }

    /// <summary>Anything the claimant wants the reviewer to read. Trust displays it, never parses it.</summary>
    public string? Notes { get; init; }
}
