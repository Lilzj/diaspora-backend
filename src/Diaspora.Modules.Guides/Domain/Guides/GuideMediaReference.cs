using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// One media asset attached to a guide, by reference.
/// <para>
/// A <see cref="MediaId"/> and never a URL — ADR-005's rule, and the reason it matters here is
/// the guide's lifespan: a guide outlives storage migrations, CDN changes and signed-URL
/// expiry, and a stored URL would break on all three while a reference survives them. The pair
/// (media, role) is the identity — attaching the same asset in the same role twice is a no-op,
/// which is what makes the attach endpoint safely retryable.
/// </para>
/// </summary>
public sealed class GuideMediaReference
{
    private GuideMediaReference(Guid mediaId, GuideMediaRole role, int order, string? caption)
    {
        MediaId = mediaId;
        Role = role;
        Order = order;
        Caption = caption;
    }

    private GuideMediaReference() { }

    public Guid MediaId { get; private set; }

    public GuideMediaRole Role { get; private set; }

    public int Order { get; private set; }

    public string? Caption { get; private set; }

    internal static GuideMediaReference Create(
        Guid mediaId, GuideMediaRole role, int order, string? caption)
    {
        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

        if (trimmed is { Length: > GuideLimits.MaxMediaCaption })
        {
            trimmed = trimmed[..GuideLimits.MaxMediaCaption];
        }

        return new GuideMediaReference(mediaId, role, order, trimmed);
    }
}
