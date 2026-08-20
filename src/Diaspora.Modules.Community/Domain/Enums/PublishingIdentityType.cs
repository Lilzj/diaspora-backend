namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Who a post is published <em>as</em>.
/// <para>
/// Names verbatim from the client, which already has an identity switcher in its creator
/// dashboard. The distinction matters to B6 in exactly two places and nowhere else: the author
/// of a post is always a real member, and the <em>publisher</em> may be an organization they
/// act for. Reputation, moderation history and blocking follow the member; display, follows and
/// organization feeds follow the publisher.
/// </para>
/// <para>
/// Collapsing the two — storing only the organization — is the mistake that makes it impossible
/// to moderate the person who actually posted.
/// </para>
/// </summary>
public enum PublishingIdentityType
{
    /// <summary>The member, as themselves. The default.</summary>
    Personal = 0,

    Organization = 1,

    Business = 2,

    /// <summary>A community group.</summary>
    Community = 3,

    /// <summary>An association Trust has verified.</summary>
    VerifiedAssociation = 4
}
