namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// What one content block is. Text kinds carry their prose in the block's content; media kinds
/// carry a media reference and use the content as a caption; Table and Links carry an opaque
/// JSON payload the editor owns.
/// </summary>
public enum GuideBlockKind
{
    Markdown = 0,
    RichText = 1,
    Image = 2,
    Video = 3,
    Table = 4,
    Links = 5,
    Callout = 6,
    Warning = 7,
    Tip = 8,
    Attachment = 9,
    CodeSnippet = 10
}
