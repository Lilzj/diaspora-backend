using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// The unfurled form of a link in a post.
/// <para>
/// B6 <b>does not fetch it</b>. Unfurling a member-supplied URL server-side is a
/// request-forgery primitive: it makes the platform issue an authenticated-from-our-network GET
/// against any address a member can type, including internal ones. Every unfurler that has
/// shipped without that in mind has been the subject of a write-up.
/// </para>
/// <para>
/// So this is a container for whatever a registered content enricher produced, and null until
/// one does. The enricher is where the allow-list, the redirect ceiling, the timeout and the
/// private-range check belong, because those are deployment decisions rather than domain ones.
/// </para>
/// <para>
/// <see cref="ImageMediaId"/> is a media reference and not an image URL, for the same reason
/// every other image on the platform is: a remote image URL rendered by a client leaks the
/// reader IP address to whoever controls it. An enricher that wants a preview image uploads it
/// through Media and stores the identifier.
/// </para>
/// </summary>
public sealed class LinkPreview : ValueObject
{
    public const int MaxUrlLength = 2_048;

    public const int MaxTitleLength = 300;

    public const int MaxDescriptionLength = 1_000;

    private LinkPreview(
        string url, string? title, string? description, string? siteName, Guid? imageMediaId)
    {
        Url = url;
        Title = title;
        Description = description;
        SiteName = siteName;
        ImageMediaId = imageMediaId;
    }

    private LinkPreview() { }

    public string Url { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public string? Description { get; private set; }

    public string? SiteName { get; private set; }

    /// <summary>A Media asset identifier, never a remote image URL.</summary>
    public Guid? ImageMediaId { get; private set; }

    public bool IsUnfurled => Title is not null || Description is not null;

    /// <summary>A bare link, as the composer submitted it. No enricher has run yet.</summary>
    public static LinkPreview Bare(string url) =>
        new(Truncate(url, MaxUrlLength), null, null, null, null);

    /// <summary>An enriched link. Only a content enricher should call this.</summary>
    public static LinkPreview Unfurled(
        string url,
        string? title,
        string? description,
        string? siteName,
        Guid? imageMediaId) =>
        new(
            Truncate(url, MaxUrlLength),
            TruncateOptional(title, MaxTitleLength),
            TruncateOptional(description, MaxDescriptionLength),
            TruncateOptional(siteName, MaxTitleLength),
            imageMediaId);

    private static string Truncate(string? value, int max)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    /// <summary>
    /// Truncates, and preserves the difference between "absent" and "empty". An enricher that
    /// found no title must leave <see cref="IsUnfurled"/> false rather than storing a blank one.
    /// </summary>
    private static string? TruncateOptional(string? value, int max)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Url;
        yield return Title;
        yield return Description;
        yield return SiteName;
        yield return ImageMediaId;
    }
}
