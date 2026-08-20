using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// A B8 guide linked beside an organization — a religious-practice explainer, a community
/// onboarding piece.
/// <para>
/// The identifier is authoritative and the title and slug are a cache, refreshed when the guide
/// changes and used so a card renders without a call into B8. A stale cached title is a cosmetic
/// problem; a missing guide is resolved as absent, because a guide that was unpublished should stop
/// being offered.
/// </para>
/// </summary>
public sealed class OrganizationGuideReference
{
    private OrganizationGuideReference(Guid guideId, string? title, string? slug)
    {
        GuideId = guideId;
        Title = title;
        Slug = slug;
    }

    private OrganizationGuideReference() { }

    public Guid GuideId { get; private set; }

    public string? Title { get; private set; }

    public string? Slug { get; private set; }

    public static OrganizationGuideReference Create(Guid guideId, string? title, string? slug) =>
        new(guideId, Clip(title, CommunityLifeLimits.MaxDisplayName), Clip(slug, CommunityLifeLimits.MaxSlug));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
