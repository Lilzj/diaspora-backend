using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// Trust reached a decision about an organization.
/// <para>
/// Published on every change of state rather than only on approval, because a revocation matters
/// more to a consumer than a grant: a directory that stopped hearing about badges being taken away
/// would keep displaying one for a body the platform no longer vouches for.
/// </para>
/// </summary>
public sealed record OrganizationVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public required string PreviousState { get; init; }

    public required string CurrentState { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>Trust's case identifier.</summary>
    public Guid? VerificationId { get; init; }

    /// <summary>Trust's own badge name, passed through and never parsed here.</summary>
    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The organization's registered administrator — B13.1. The member who created it.
    /// <para>
    /// <b>Trust decided this, not the organization.</b> Both directions are news it learns rather
    /// than causes, and <see cref="Reason"/> is written for this member — never for the
    /// directory.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the organization could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}
