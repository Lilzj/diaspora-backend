using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// One document a candidate sent with an application.
/// <para>
/// A B5 reference and never bytes. The virus scan, the storage, the access policy and the signed
/// URL an employer downloads through are all Media's; B9 stores an identifier, a kind and the
/// file name Media reported.
/// </para>
/// <para>
/// <b>The attachment list is frozen once the application is submitted.</b> An employer reviewing
/// a document must be looking at what they were sent, and a candidate who could swap a résumé
/// after a rejection would make the record worthless as evidence for either of them.
/// </para>
/// </summary>
public sealed class ApplicationAttachment
{
    private ApplicationAttachment(
        Guid attachmentId,
        ApplicationAttachmentKind kind,
        Guid mediaId,
        string? fileName,
        int order)
    {
        AttachmentId = attachmentId;
        Kind = kind;
        MediaId = mediaId;
        FileName = fileName;
        Order = order;
    }

    private ApplicationAttachment() { }

    public Guid AttachmentId { get; private set; }

    public ApplicationAttachmentKind Kind { get; private set; }

    /// <summary>B5's reference. Never a URL.</summary>
    public Guid MediaId { get; private set; }

    /// <summary>The name Media reported, shown beside the download.</summary>
    public string? FileName { get; private set; }

    public int Order { get; private set; }

    internal static ApplicationAttachment? Create(
        Guid attachmentId,
        ApplicationAttachmentKind kind,
        Guid mediaId,
        string? fileName,
        int order)
    {
        if (mediaId == Guid.Empty || !Enum.IsDefined(kind))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();

        if (name is { Length: > JobLimits.MaxDisplayName })
        {
            name = name[..JobLimits.MaxDisplayName].TrimEnd();
        }

        return new ApplicationAttachment(
            attachmentId == Guid.Empty ? Guid.NewGuid() : attachmentId,
            kind, mediaId, name, order);
    }
}
