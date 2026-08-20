namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Why somebody reported a listing.
/// <para>
/// A closed set rather than free text, because the queue is worked by reason: fraud is triaged in
/// minutes and a miscategorisation in days, and a moderator reading a thousand free-text reports
/// cannot tell them apart at a glance. The reporter's own words go in a note beside this.
/// </para>
/// </summary>
public enum ListingReportReason
{
    /// <summary>A scam. The highest-priority reason this module carries.</summary>
    Fraud = 0,

    /// <summary>The same item listed more than once.</summary>
    Duplicate = 1,

    Spam = 2,

    /// <summary>Something that may not be sold here at all.</summary>
    Prohibited = 3,

    /// <summary>Passed off as a brand it is not.</summary>
    Counterfeit = 4,

    /// <summary>In the wrong category. Corrected rather than removed.</summary>
    Miscategorised = 5,

    Offensive = 6,

    Other = 7
}
