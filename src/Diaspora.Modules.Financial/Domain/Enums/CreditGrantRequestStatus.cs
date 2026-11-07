namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Where a credit grant that needs a second Admin has got to (7 Nov 2026).</summary>
public enum CreditGrantRequestStatus
{
    /// <summary>Asked for. Nothing has been granted.</summary>
    Pending = 1,

    /// <summary>A second Admin agreed, and the credit landed then.</summary>
    Approved = 2,

    /// <summary>Another Admin declined it, with a note.</summary>
    Rejected = 3,

    /// <summary>The Admin who asked took it back before anybody decided.</summary>
    Withdrawn = 4
}
