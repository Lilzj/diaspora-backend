namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Ask Trust to vouch for an organization.
/// <para>
/// Returns a case identifier, never a decision. Approval, rejection, revocation and badge
/// assignment are Trust's alone.
/// </para>
/// </summary>
public sealed record SubmitOrganizationVerificationRequest
{
    /// <summary>B5 asset identifiers for the evidence — a constitution, a registration certificate.</summary>
    public IReadOnlyList<Guid> EvidenceMediaIds { get; init; } = [];

    /// <summary>The registered name, when it differs from the display name.</summary>
    public string? LegalName { get; init; }

    /// <summary>A charity or company number. Displayed to a reviewer and never parsed.</summary>
    public string? RegistrationNumber { get; init; }

    public string? Notes { get; init; }
}
