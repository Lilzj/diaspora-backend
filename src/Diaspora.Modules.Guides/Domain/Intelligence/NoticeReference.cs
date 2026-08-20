using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// One thing a notice points at — a guide explaining the new rule, the embassy that issued it, an
/// outreach event where somebody can ask about it.
/// <para>
/// <b>A kind and an identifier, and that is the whole row.</b> This is B6's
/// <c>ContentReference</c> lesson in a different context: the alternative was eight owned
/// collections, eight tables, eight limit checks and eight places to forget one, for a set that is
/// only ever read as "what else should I look at". The kind is what tells a client which module to
/// resolve the identifier against.
/// </para>
/// <para>
/// <b>Never a copy.</b> No title, no status, no summary. A notice that cached a job title would go
/// on showing it after the job closed, and a notice that cached a guide's title would silently
/// rewrite itself when the author retitled — which for a document whose whole value is that it says
/// what an authority said is the worst possible failure. The label is the <em>editor's</em> framing
/// of why the link is there, which is a fact about this notice rather than about the target.
/// </para>
/// </summary>
public sealed class NoticeReference
{
    private NoticeReference(
        NoticeReferenceKind kind, Guid targetId, string? label, int position)
    {
        Kind = kind;
        TargetId = targetId;
        Label = label;
        Position = position;
    }

    private NoticeReference() { }

    /// <summary>Which module owns the target. Half of the row's identity.</summary>
    public NoticeReferenceKind Kind { get; private set; }

    /// <summary>The target's identifier, as a primitive. Half of the row's identity.</summary>
    public Guid TargetId { get; private set; }

    /// <summary>Why the editor linked it. The editor's words about this notice, not the target's.</summary>
    public string? Label { get; private set; }

    /// <summary>Display order within the notice. Contiguous from zero.</summary>
    public int Position { get; private set; }

    internal static NoticeReference? TryCreate(
        NoticeReferenceKind kind, Guid targetId, string? label, int position) =>
        targetId == Guid.Empty || !Enum.IsDefined(kind)
            ? null
            : new NoticeReference(
                kind,
                targetId,
                IntelligenceNormalization.Text(label, IntelligenceLimits.MaxReferenceLabel),
                position);
}
