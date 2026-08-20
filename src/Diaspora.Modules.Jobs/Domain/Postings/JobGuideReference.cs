using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// A guide linked beside a posting — the work-permit explainer, the interview-tips piece, the
/// country employment guide, the salary-negotiation walkthrough.
/// <para>
/// <b>This is the contextual link that makes the two modules worth having together.</b> A
/// posting in Germany that sponsors a visa is exactly the moment a candidate needs B8's
/// German work-permit guide, and putting it on the posting means they get it without knowing
/// the knowledge base exists.
/// </para>
/// <para>
/// An identifier and a label only. The guide's title, its verification badge and whether it is
/// still published are B8's answers at read time, through <c>IGuideDirectory</c> — a posting
/// must never render a link to a guide that has since been unpublished, and a copied title
/// would do exactly that.
/// </para>
/// <para>
/// <see cref="IsAutomatic"/> distinguishes a link the employer chose from one the platform
/// suggested by matching country and topic. Both are shown; only the employer's survives an
/// edit that clears the list, so a suggestion can be refreshed without discarding a choice.
/// </para>
/// </summary>
public sealed class JobGuideReference
{
    private JobGuideReference(Guid guideId, string? label, bool isAutomatic, int order)
    {
        GuideId = guideId;
        Label = label;
        IsAutomatic = isAutomatic;
        Order = order;
    }

    private JobGuideReference() { }

    /// <summary>B8's identifier. A value, not a foreign key.</summary>
    public Guid GuideId { get; private set; }

    /// <summary>Why this guide is here — "How to get a Blue Card". Rendered beside the link.</summary>
    public string? Label { get; private set; }

    /// <summary>Whether the platform suggested this rather than the employer choosing it.</summary>
    public bool IsAutomatic { get; private set; }

    public int Order { get; private set; }

    internal static JobGuideReference Create(
        Guid guideId, string? label, bool isAutomatic, int order)
    {
        var trimmed = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        if (trimmed is { Length: > JobLimits.MaxDisplayName })
        {
            trimmed = trimmed[..JobLimits.MaxDisplayName].TrimEnd();
        }

        return new JobGuideReference(guideId, trimmed, isAutomatic, order);
    }
}
