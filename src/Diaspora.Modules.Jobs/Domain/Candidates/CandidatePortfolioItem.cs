using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One piece of a candidate's portfolio — a link to work they have done, or an uploaded sample.
/// <para>
/// <b>This is the module's "Portfolio", refined from the brief's aggregate into an owned
/// collection.</b> A portfolio has no lifecycle of its own, is never referenced from outside
/// the profile, and is always loaded with it; giving it a separate aggregate root would buy an
/// extra table and a join and cost the ability to validate the set as a whole. Résumés, which
/// genuinely are attached to applications independently and versioned over time, are a separate
/// aggregate — that is where the line actually falls.
/// </para>
/// <para>
/// A link or a media reference, and at least one of the two. URLs are pinned to http(s): a
/// profile is exactly the surface someone would use to smuggle a redirect past a recruiter.
/// </para>
/// </summary>
public sealed class CandidatePortfolioItem
{
    private CandidatePortfolioItem(
        Guid itemId, string label, string? url, Guid? mediaId, string? description, int order)
    {
        ItemId = itemId;
        Label = label;
        Url = url;
        MediaId = mediaId;
        Description = description;
        Order = order;
    }

    private CandidatePortfolioItem() { }

    public Guid ItemId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    /// <summary>An absolute http(s) link. Null when the item is an upload.</summary>
    public string? Url { get; private set; }

    /// <summary>A B5 reference to an uploaded sample. Null when the item is a link.</summary>
    public Guid? MediaId { get; private set; }

    public string? Description { get; private set; }

    public int Order { get; private set; }

    internal static CandidatePortfolioItem? Create(
        Guid itemId, string? label, string? url, Guid? mediaId, string? description, int order)
    {
        var title = Clip(label, JobLimits.MaxDisplayName);

        if (title is null)
        {
            return null;
        }

        string? link = null;
        var raw = Clip(url, JobLimits.MaxUrl);

        if (raw is not null)
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            link = uri.ToString();
        }

        var asset = mediaId == Guid.Empty ? null : mediaId;

        // An item that is neither a link nor an upload points at nothing.
        if (link is null && asset is null)
        {
            return null;
        }

        return new CandidatePortfolioItem(
            itemId == Guid.Empty ? Guid.NewGuid() : itemId,
            title,
            link,
            asset,
            Clip(description, JobLimits.MaxBulletLength),
            order);
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
