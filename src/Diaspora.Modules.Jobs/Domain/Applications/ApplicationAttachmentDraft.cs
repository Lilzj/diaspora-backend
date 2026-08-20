using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>One attachment as the application form sends it. A Media reference, never a file.</summary>
public sealed record ApplicationAttachmentDraft
{
    public required ApplicationAttachmentKind Kind { get; init; }

    /// <summary>B5's reference to the already-uploaded document.</summary>
    public required Guid MediaId { get; init; }

    public string? FileName { get; init; }
}
