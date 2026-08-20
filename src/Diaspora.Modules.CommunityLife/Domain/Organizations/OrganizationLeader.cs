using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// Somebody named on an organization page — a pastor, an imam, a chairperson, a treasurer.
/// <para>
/// <b>Deliberately not a membership.</b> The person a church puts on its page may not have an
/// account on this platform, and requiring one would leave every organization unable to say who
/// runs it. When a leader does have an account, <see cref="MemberUserId"/> links the two and the
/// client renders a real profile; when they do not, the name and role still render.
/// </para>
/// <para>
/// The photograph is a Media reference like every other image in this module. It is on the leader
/// rather than in the gallery because it is rendered beside the name and would be meaningless in a
/// grid.
/// </para>
/// </summary>
public sealed class OrganizationLeader : Entity<Guid>
{
    private OrganizationLeader(
        Guid id, string name, string? role, Guid? memberUserId, Guid? photoMediaId, int order)
        : base(id)
    {
        Name = name;
        Role = role;
        MemberUserId = memberUserId;
        PhotoMediaId = photoMediaId;
        Order = order;
    }

    private OrganizationLeader() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>What they are called in the organization: Pastor, Imam, Chair, Secretary.</summary>
    public string? Role { get; private set; }

    /// <summary>Their platform account, when they have one. Optional by design.</summary>
    public Guid? MemberUserId { get; private set; }

    /// <summary>A B5 asset identifier. Never a URL.</summary>
    public Guid? PhotoMediaId { get; private set; }

    /// <summary>Where they appear in the list. Leadership order is meaningful.</summary>
    public int Order { get; private set; }

    public static OrganizationLeader Create(
        Guid id,
        string? name,
        string? role = null,
        Guid? memberUserId = null,
        Guid? photoMediaId = null,
        int order = 0) =>
        new(
            id,
            Clip(name, CommunityLifeLimits.MaxDisplayName) ?? string.Empty,
            Clip(role, CommunityLifeLimits.MaxDisplayName),
            memberUserId == Guid.Empty ? null : memberUserId,
            photoMediaId == Guid.Empty ? null : photoMediaId,
            Math.Max(0, order));

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
