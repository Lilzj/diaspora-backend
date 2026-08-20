namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Asks Trust to verify a landlord or a property.
/// <para>
/// A request, never a decision. Housing lodges the claim and Trust decides; nothing in this module
/// can set a mark to verified.
/// </para>
/// </summary>
public sealed record SubmitHousingVerificationRequest
{
    /// <summary>Media assets Trust will show a reviewer. Identifiers, never bytes.</summary>
    public IReadOnlyList<Guid>? EvidenceMediaIds { get; init; }

    /// <summary>Anything the claimant wants the reviewer to read.</summary>
    public string? Notes { get; init; }
}
