namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// Where one verification claim currently stands, as B7 last heard from Trust.
/// <para>
/// <b>A projection, never a decision.</b> Trust owns whether a provider is verified; this enum
/// records what Trust said. Nothing in B7 may move a claim to <see cref="Verified"/> except an
/// event handler reacting to a Trust integration event, and the one method that does
/// (<c>ProviderVerification.Grant</c>) is internal to the aggregate for exactly that reason.
/// ADR-002 draws this line for Identity and Trust; B7 is on the same side of it as Location's
/// residency projection.
/// </para>
/// <para>
/// A deliberately smaller vocabulary than Trust's twelve-member <c>VerificationStatus</c>.
/// A provider card renders a badge or it does not; the difference between Trust's
/// <c>UnderReview</c>, <c>NeedMoreInfo</c> and <c>UnderInvestigation</c> matters to the member
/// in the verification wizard and to nobody looking at a restaurant. The finer states stay in
/// Trust, where the wizard reads them.
/// </para>
/// </summary>
public enum ProviderVerificationState
{
    /// <summary>Never claimed. The default for every new provider.</summary>
    Unverified = 0,

    /// <summary>Submitted to Trust and awaiting an outcome. Trust's Submitted/Pending/UnderReview/NeedMoreInfo.</summary>
    Pending = 1,

    /// <summary>Granted and current. Trust's Approved and PartiallyApproved.</summary>
    Verified = 2,

    /// <summary>Refused. Trust's Rejected.</summary>
    Rejected = 3,

    /// <summary>Was granted; the evidence has lapsed. Trust's Expired.</summary>
    Expired = 4,

    /// <summary>Was granted; withdrawn for cause. Trust's Revoked.</summary>
    Revoked = 5
}
