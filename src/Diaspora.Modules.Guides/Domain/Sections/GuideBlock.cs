using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Sections;

/// <summary>
/// One unit of content inside a section.
/// <para>
/// The kind decides what the fields mean: text kinds carry prose in <see cref="Content"/>;
/// media kinds carry the asset in <see cref="MediaId"/> and use the content as a caption —
/// a reference and never a URL, per ADR-005; Table and Links carry an opaque JSON payload in
/// <see cref="Metadata"/> that the editor owns and the domain deliberately does not parse.
/// </para>
/// </summary>
public sealed class GuideBlock : Entity<Guid>
{
    private static readonly char[] WordSeparators = [' ', '\t', '\r', '\n'];

    private GuideBlock(
        Guid id, GuideBlockKind kind, int order, string? content, Guid? mediaId, string? metadata)
        : base(id)
    {
        Kind = kind;
        Order = order;
        Content = content;
        MediaId = mediaId;
        Metadata = metadata;
    }

    private GuideBlock() { }

    public GuideBlockKind Kind { get; private set; }

    public int Order { get; private set; }

    /// <summary>The prose, or the caption for a media-kind block.</summary>
    public string? Content { get; private set; }

    /// <summary>The referenced asset, for media-kind blocks. A reference, never a URL.</summary>
    public Guid? MediaId { get; private set; }

    /// <summary>Opaque JSON for table and link payloads.</summary>
    public string? Metadata { get; private set; }

    /// <summary>Whether a reader would see anything here. Feeds the publishability check.</summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(Content) || MediaId is not null
        || !string.IsNullOrWhiteSpace(Metadata);

    /// <summary>Whether the block carries a referenced media asset.</summary>
    public bool IsMediaKind =>
        Kind is GuideBlockKind.Image or GuideBlockKind.Video or GuideBlockKind.Attachment;

    /// <summary>
    /// Words a reader actually reads. Prose kinds only — captions, table payloads and code are
    /// excluded, because counting a data table's JSON as reading material inflates the reading
    /// time of exactly the guides that are quickest to use.
    /// </summary>
    public int WordCount =>
        Kind is GuideBlockKind.Markdown or GuideBlockKind.RichText or GuideBlockKind.Callout
            or GuideBlockKind.Warning or GuideBlockKind.Tip
        && Content is { } content
            ? content.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length
            : 0;

    internal static Result<GuideBlock> Create(GuideBlockDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.BlockId == Guid.Empty)
        {
            return Result.Failure<GuideBlock>(GuideErrors.BlockInvalid);
        }

        var content = string.IsNullOrWhiteSpace(draft.Content) ? null : draft.Content.Trim();

        if (content is { Length: > GuideLimits.MaxBlockLength })
        {
            return Result.Failure<GuideBlock>(GuideErrors.BlockInvalid);
        }

        var block = new GuideBlock(
            draft.BlockId, draft.Kind, draft.Order, content, draft.MediaId, draft.Metadata);

        // A media block with no media is a broken image on every reader's screen; catching it
        // at save keeps the failure on the author's screen instead.
        return block.IsMediaKind && draft.MediaId is null
            ? Result.Failure<GuideBlock>(GuideErrors.BlockInvalid)
            : Result.Success(block);
    }
}
