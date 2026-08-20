namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What a member followed.
/// <para>
/// The follow graph lives in B6 rather than in Identity, and the reason is this enum. Following
/// is not a fact about who someone is; it is a subscription to content, and every feed that
/// consumes it is B6. Identity owns the member; B6 owns whom they read.
/// </para>
/// </summary>
public enum FollowTargetType
{
    Member = 0,

    Organization = 1,

    /// <summary>Following a hashtag. The subscription that makes a hashtag feed personal.</summary>
    Hashtag = 2
}
