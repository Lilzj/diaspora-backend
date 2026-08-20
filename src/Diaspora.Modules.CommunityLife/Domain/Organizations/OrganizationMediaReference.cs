using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// A B5 asset attached to an organization, by reference.
/// <para>
/// <b>An identifier and a role. Never a URL, never bytes.</b> ADR-005 is the rule and an
/// architecture test enforces it: Media owns the bytes, the variants, the stripped EXIF and the
/// signed URL, and a module that cached a URL would serve a broken image the first time B5 rotated
/// its storage.
/// </para>
/// </summary>
public sealed class OrganizationMediaReference
{
    private OrganizationMediaReference(
        Guid mediaId, CommunityLifeMediaRole role, int order, string? caption)
    {
        MediaId = mediaId;
        Role = role;
        Order = order;
        Caption = caption;
    }

    private OrganizationMediaReference() { }

    public Guid MediaId { get; private set; }

    public CommunityLifeMediaRole Role { get; private set; }

    public int Order { get; private set; }

    public string? Caption { get; private set; }

    /// <summary>Whether the asset is a file to download rather than an image to render.</summary>
    public bool IsDocument =>
        Role is CommunityLifeMediaRole.Document
            or CommunityLifeMediaRole.Brochure
            or CommunityLifeMediaRole.VerificationEvidence;

    public static OrganizationMediaReference Create(
        Guid mediaId, CommunityLifeMediaRole role, int order, string? caption)
    {
        var trimmed = caption?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxDisplayName })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxDisplayName];
        }

        return new OrganizationMediaReference(
            mediaId, role, Math.Max(0, order), string.IsNullOrEmpty(trimmed) ? null : trimmed);
    }
}
