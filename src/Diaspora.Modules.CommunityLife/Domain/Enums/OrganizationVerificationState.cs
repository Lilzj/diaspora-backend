namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What Trust has decided about an organization.
/// <para>
/// A projection of a decision this module cannot make. Nothing in Community Life can move this to
/// <see cref="Verified"/> except the handler that receives Trust's published outcome.
/// </para>
/// </summary>
public enum OrganizationVerificationState
{
    /// <summary>Nobody has asked. The state every organization starts in.</summary>
    Unverified = 0,

    /// <summary>A case is open with Trust.</summary>
    Pending = 1,

    /// <summary>Trust vouched for it.</summary>
    Verified = 2,

    /// <summary>Trust refused.</summary>
    Rejected = 3,

    /// <summary>Trust took the badge back.</summary>
    Revoked = 4,

    /// <summary>The badge lapsed and has not been renewed.</summary>
    Expired = 5
}
