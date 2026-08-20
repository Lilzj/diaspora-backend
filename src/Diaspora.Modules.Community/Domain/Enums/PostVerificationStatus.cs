namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Whether the community has corroborated a post.
/// <para>
/// Names verbatim from the client. Worth being precise about what this is <b>not</b>: it is not
/// a Trust decision. Trust answers whether a member has proved who they are; this answers
/// whether enough members have said a post is true. B6 owns the second question entirely, and
/// consumes Trust only to decide whether a given confirmation counts toward the
/// verified-resident quota.
/// </para>
/// </summary>
public enum PostVerificationStatus
{
    /// <summary>Below the confirmation threshold for its urgency.</summary>
    Unverified = 0,

    /// <summary>Enough confirmations, including enough from verified residents.</summary>
    CommunityVerified = 1,

    /// <summary>Confirmations and disputes are both material. Ranked down, not hidden.</summary>
    Disputed = 2
}
