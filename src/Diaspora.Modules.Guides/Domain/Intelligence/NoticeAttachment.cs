using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// A media reference attached to a notice — the scanned circular, the new application form, the map
/// of the affected district.
/// <para>
/// <b>A reference, never a URL.</b> ADR-005 is the platform's oldest rule about media and it applies
/// with more force here than anywhere: an official document is exactly the asset whose storage
/// location will move, whose access will need to be revoked, and whose delivery a deployment will
/// want to put behind a signed URL. A notice that stored <c>https://cdn/…/circular.pdf</c> would
/// have frozen all three decisions at the moment somebody uploaded a PDF.
/// </para>
/// <para>
/// The caption is the accessible description, and it is the aggregate's only editorial content about
/// the asset. Everything else — dimensions, content type, moderation state — belongs to B5 and is
/// resolved at read time.
/// </para>
/// </summary>
public sealed class NoticeAttachment
{
    private NoticeAttachment(Guid mediaId, string? caption, int position)
    {
        MediaId = mediaId;
        Caption = caption;
        Position = position;
    }

    private NoticeAttachment() { }

    /// <summary>A B5 media identifier. Half of the row's identity, and never resolved here.</summary>
    public Guid MediaId { get; private set; }

    /// <summary>What the asset is, in words. Rendered verbatim, never interpreted.</summary>
    public string? Caption { get; private set; }

    /// <summary>Display order. Contiguous from zero, renumbered on every change.</summary>
    public int Position { get; private set; }

    internal static NoticeAttachment? TryCreate(Guid mediaId, string? caption, int position) =>
        mediaId == Guid.Empty
            ? null
            : new NoticeAttachment(
                mediaId,
                IntelligenceNormalization.Text(caption, IntelligenceLimits.MaxReferenceLabel),
                position);
}
