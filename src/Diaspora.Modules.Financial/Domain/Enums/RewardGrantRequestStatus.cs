namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Where a staff grant that needs a second Admin has got to (RWD-10).</summary>
public enum RewardGrantRequestStatus
{
    /// <summary>Asked for. Nothing has been granted.</summary>
    Pending = 1,

    /// <summary>A second Admin agreed, and the grant landed then.</summary>
    Approved = 2,

    /// <summary>Another Admin declined it, with a note.</summary>
    Rejected = 3,

    /// <summary>The Admin who asked took it back before anybody decided.</summary>
    Withdrawn = 4
}
