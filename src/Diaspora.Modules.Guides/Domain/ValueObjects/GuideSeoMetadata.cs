using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// What search engines are told about a guide, when the author chooses to say anything.
/// <para>
/// Every member is nullable and the whole block is optional in spirit — the platform derives
/// sensible defaults from the title and summary — but the navigation is always present so the
/// read path never branches on a missing object. The canonical URL must be absolute https,
/// because a relative or http canonical is worse than none: it tells a crawler the guide's
/// authoritative home is somewhere it is not.
/// </para>
/// </summary>
public sealed class GuideSeoMetadata : ValueObject
{
    private GuideSeoMetadata(
        string? metaTitle, string? metaDescription, string? canonicalUrl, string? keywords)
    {
        MetaTitle = metaTitle;
        MetaDescription = metaDescription;
        CanonicalUrl = canonicalUrl;
        Keywords = keywords;
    }

    private GuideSeoMetadata() { }

    /// <summary>A block with nothing said. What every new guide starts with.</summary>
    public static GuideSeoMetadata Empty => new();

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public string? CanonicalUrl { get; private set; }

    /// <summary>Comma-joined keywords, kept as the author typed them.</summary>
    public string? Keywords { get; private set; }

    public static Result<GuideSeoMetadata> Create(
        string? metaTitle, string? metaDescription, string? canonicalUrl, string? keywords)
    {
        var title = Trim(metaTitle);
        var description = Trim(metaDescription);
        var canonical = Trim(canonicalUrl);
        var joinedKeywords = Trim(keywords);

        if (title is { Length: > GuideLimits.MaxSeoTitle }
            || description is { Length: > GuideLimits.MaxSeoDescription }
            || joinedKeywords is { Length: > GuideLimits.MaxSeoKeywords })
        {
            return Result.Failure<GuideSeoMetadata>(GuideErrors.SeoInvalid);
        }

        if (canonical is not null
            && (canonical.Length > GuideLimits.MaxSeoUrl
                || !Uri.TryCreate(canonical, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<GuideSeoMetadata>(GuideErrors.SeoInvalid);
        }

        return Result.Success(new GuideSeoMetadata(title, description, canonical, joinedKeywords));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MetaTitle;
        yield return MetaDescription;
        yield return CanonicalUrl;
        yield return Keywords;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
