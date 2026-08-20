using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// A B5 asset attached to an event, by reference.
/// <para>
/// An identifier and a role. Never a URL, never bytes — ADR-005, enforced by an architecture test.
/// </para>
/// </summary>
public sealed class EventMediaReference
{
    private EventMediaReference(
        Guid mediaId, CommunityLifeMediaRole role, int order, string? caption)
    {
        MediaId = mediaId;
        Role = role;
        Order = order;
        Caption = caption;
    }

    private EventMediaReference() { }

    public Guid MediaId { get; private set; }

    public CommunityLifeMediaRole Role { get; private set; }

    public int Order { get; private set; }

    public string? Caption { get; private set; }

    public bool IsDocument =>
        Role is CommunityLifeMediaRole.Document
            or CommunityLifeMediaRole.Brochure
            or CommunityLifeMediaRole.VerificationEvidence;

    public static EventMediaReference Create(
        Guid mediaId, CommunityLifeMediaRole role, int order, string? caption)
    {
        var trimmed = caption?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxDisplayName })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxDisplayName];
        }

        return new EventMediaReference(
            mediaId, role, Math.Max(0, order), string.IsNullOrEmpty(trimmed) ? null : trimmed);
    }
}
