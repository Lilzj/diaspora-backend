namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What Trust has said about a landlord or a property.
/// <para>
/// <b>Every value here is written by Trust and read by Housing.</b> Nothing in this module may
/// move a mark to <see cref="Verified"/> — that is B3's decision, arriving on B3's published
/// outcome event. Housing may only ask.
/// </para>
/// </summary>
public enum HousingVerificationState
{
    /// <summary>Never submitted.</summary>
    Unverified = 0,

    /// <summary>Submitted and awaiting Trust's decision.</summary>
    Pending = 1,

    /// <summary>Trust vouched for it.</summary>
    Verified = 2,

    /// <summary>Trust refused. Resubmission is allowed.</summary>
    Rejected = 3,

    /// <summary>Trust withdrew a verification it had granted.</summary>
    Revoked = 4
}
