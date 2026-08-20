namespace Diaspora.Modules.Guides.Domain.Visas;

/// <summary>
/// A guide that explains how to actually do what an advisory describes.
/// <para>
/// <b>An identifier and a position, and nothing else.</b> This is the same rule every reference in
/// this phase follows and the same one B8.1's curated shelves follow: the advisory names a guide and
/// the read path resolves it through B8's own directory contract, with B8's visibility rules. An
/// advisory that cached the guide's title would keep showing the old one after a retitle and would
/// go on linking a guide its author had unpublished.
/// </para>
/// <para>
/// A separate collection from <c>NoticeReference</c> rather than a shared one, because an advisory
/// only ever links guides. The brief lists exactly that, and a general reference model here would be
/// eight kinds of link on a record where seven of them have never been asked for.
/// </para>
/// </summary>
public sealed class VisaGuideReference
{
    private VisaGuideReference(Guid guideId, int position)
    {
        GuideId = guideId;
        Position = position;
    }

    private VisaGuideReference() { }

    /// <summary>A B8 guide identifier, as a primitive. The row's identity within its advisory.</summary>
    public Guid GuideId { get; private set; }

    /// <summary>Display order. Contiguous from zero.</summary>
    public int Position { get; private set; }

    internal static VisaGuideReference? TryCreate(Guid guideId, int position) =>
        guideId == Guid.Empty ? null : new VisaGuideReference(guideId, position);
}
