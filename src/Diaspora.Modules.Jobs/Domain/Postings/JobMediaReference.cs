using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One media asset attached to a posting or an employer, by reference.
/// <para>
/// A <see cref="MediaId"/> and never a URL — ADR-005's rule. It matters here because a posting
/// outlives storage migrations, CDN changes and signed-URL expiry, and a stored URL breaks on
/// all three while a reference survives them. The bytes, the virus scan, the access policy and
/// the signed URL are all B5's.
/// </para>
/// <para>
/// The pair (media, role) is the identity — attaching the same asset in the same role twice is
/// a no-op, which is what makes the attach endpoint safely retryable.
/// </para>
/// </summary>
public sealed class JobMediaReference
{
    private JobMediaReference(Guid mediaId, JobMediaRole role, int order, string? caption)
    {
        MediaId = mediaId;
        Role = role;
        Order = order;
        Caption = caption;
    }

    private JobMediaReference() { }

    public Guid MediaId { get; private set; }

    public JobMediaRole Role { get; private set; }

    public int Order { get; private set; }

    public string? Caption { get; private set; }

    internal static JobMediaReference Create(
        Guid mediaId, JobMediaRole role, int order, string? caption)
    {
        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

        if (trimmed is { Length: > JobLimits.MaxDisplayName })
        {
            trimmed = trimmed[..JobLimits.MaxDisplayName];
        }

        return new JobMediaReference(mediaId, role, order, trimmed);
    }
}
