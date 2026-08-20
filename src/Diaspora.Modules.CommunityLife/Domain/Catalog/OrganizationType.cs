using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Catalog;

/// <summary>
/// A kind of organization — church, mosque, alumni association, sports club.
/// <para>
/// <b>A table rather than an enum, and that is what "categories must remain configurable"
/// costs.</b> An enum would make adding a kind of organization a code change, a migration and a
/// release; a table seeded by contributors makes it an assembly that ships, and a deployment
/// serving a community this platform has not met yet can add what that community actually has.
/// </para>
/// <para>
/// <b>Retired rather than deleted.</b> Organizations store the key, so removing a row would leave
/// pages pointing at a type that no longer exists. Retiring hides it from the picker and leaves
/// every page that already uses it intact.
/// </para>
/// </summary>
public sealed class OrganizationType : Entity<OrganizationTypeId>, IAuditableEntity
{
    private OrganizationType(
        OrganizationTypeId id,
        string key,
        string name,
        string? description,
        string? iconName,
        string? groupName,
        int order,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        IconName = iconName;
        GroupName = groupName;
        Order = order;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private OrganizationType() { }

    /// <summary>The stable key organizations store. Normalized, and never changed once published.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The client's own icon name. Rendered, never interpreted here.</summary>
    public string? IconName { get; private set; }

    /// <summary>
    /// What the picker groups it under — "Faith", "Education", "Professional". Free text, because
    /// the grouping that makes sense differs by market.
    /// </summary>
    public string? GroupName { get; private set; }

    public int Order { get; private set; }

    /// <summary>Whether it appears in the picker. Retired types stay for the pages that use them.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether organizations of this kind should be encouraged to verify.
    /// <para>
    /// A hint rather than a rule. A church or an embassy-affiliated body carries more weight when
    /// verified than a book club, and the client renders the prompt accordingly — but nothing here
    /// blocks publication, because a verification requirement would exclude exactly the small
    /// grassroots groups this platform is for.
    /// </para>
    /// </summary>
    public bool EncouragesVerification { get; private set; }

    public int OrganizationCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static OrganizationType Create(
        OrganizationTypeId id,
        string? key,
        string? name,
        DateTimeOffset createdAt,
        string? description = null,
        string? iconName = null,
        string? groupName = null,
        int order = 0,
        bool encouragesVerification = false)
    {
        var normalized = Organization.NormalizeKey(key) ?? "other";

        return new OrganizationType(
            id,
            normalized,
            Clip(name, CommunityLifeLimits.MaxDisplayName) ?? normalized,
            Clip(description, CommunityLifeLimits.MaxNoteLength),
            Clip(iconName, CommunityLifeLimits.MaxDisplayName),
            Clip(groupName, CommunityLifeLimits.MaxDisplayName),
            Math.Max(0, order),
            createdAt)
        {
            EncouragesVerification = encouragesVerification
        };
    }

    /// <summary>Restates the presentation. The key never moves.</summary>
    public void Describe(
        string? name,
        string? description,
        string? iconName,
        string? groupName,
        int? order,
        bool? encouragesVerification,
        DateTimeOffset now)
    {
        Name = Clip(name, CommunityLifeLimits.MaxDisplayName) ?? Name;
        Description = Clip(description, CommunityLifeLimits.MaxNoteLength) ?? Description;
        IconName = Clip(iconName, CommunityLifeLimits.MaxDisplayName) ?? IconName;
        GroupName = Clip(groupName, CommunityLifeLimits.MaxDisplayName) ?? GroupName;

        if (order is { } value)
        {
            Order = Math.Max(0, value);
        }

        if (encouragesVerification is { } encourage)
        {
            EncouragesVerification = encourage;
        }

        ModifiedAt = now;
    }

    public void Retire(DateTimeOffset now)
    {
        IsActive = false;
        ModifiedAt = now;
    }

    public void Reinstate(DateTimeOffset now)
    {
        IsActive = true;
        ModifiedAt = now;
    }

    /// <summary>Keeps the picker's counts honest. Settled from the organization status handler.</summary>
    public void AdjustCount(int delta, DateTimeOffset now)
    {
        OrganizationCount = Math.Max(0, OrganizationCount + delta);
        ModifiedAt = now;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
