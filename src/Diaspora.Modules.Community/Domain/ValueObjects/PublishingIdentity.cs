using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// Who a piece of content is published as.
/// <para>
/// Held alongside the author, never instead of it. The author is the member accountable for the
/// content — the one whose reputation moves, whose moderation history grows, and who a block
/// applies to. The publishing identity is who the content is attributed to on screen.
/// </para>
/// <para>
/// For most posts they are the same member and this carries <see cref="PublishingIdentityType.Personal"/>
/// with a null organization. Storing only the organization for organizational posts is the
/// mistake that makes it impossible to moderate the person who actually pressed publish.
/// </para>
/// </summary>
public sealed class PublishingIdentity : ValueObject
{
    private PublishingIdentity(PublishingIdentityType type, Guid? organizationId, string? displayName)
    {
        Type = type;
        OrganizationId = organizationId;
        DisplayName = displayName;
    }

    private PublishingIdentity() { }

    public PublishingIdentityType Type { get; private set; }

    /// <summary>Null when the member posts as themselves.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>
    /// A denormalised display name, captured at publish time.
    /// <para>
    /// Denormalised on purpose. An organization that renames itself should not retroactively
    /// rewrite the byline on three years of posts, and a feed that had to join to an
    /// organization directory to render a byline would make every feed query cross a module
    /// boundary.
    /// </para>
    /// </summary>
    public string? DisplayName { get; private set; }

    public bool IsPersonal => Type == PublishingIdentityType.Personal;

    public static PublishingIdentity Personal() =>
        new(PublishingIdentityType.Personal, null, null);

    public static PublishingIdentity ForOrganization(
        PublishingIdentityType type, Guid organizationId, string? displayName) =>
        new(type == PublishingIdentityType.Personal ? PublishingIdentityType.Organization : type,
            organizationId,
            displayName);

    /// <summary>The identifier a follow or an organization feed keys on.</summary>
    public Guid ResolveFeedOwner(UserId authorId)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        return OrganizationId ?? authorId.Value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return OrganizationId;
        yield return DisplayName;
    }
}
