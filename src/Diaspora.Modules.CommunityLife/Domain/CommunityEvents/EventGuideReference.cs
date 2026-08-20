using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// A B8 guide linked beside an event — festival guides, event etiquette, cultural introductions.
/// <para>
/// The identifier is authoritative and the title and slug are a cache, so a card renders without a
/// call into B8. This is the whole of what the brief's "link contextual guides" requirement costs:
/// no guide content is copied and no guide logic is duplicated.
/// </para>
/// </summary>
public sealed class EventGuideReference
{
    private EventGuideReference(Guid guideId, string? title, string? slug)
    {
        GuideId = guideId;
        Title = title;
        Slug = slug;
    }

    private EventGuideReference() { }

    public Guid GuideId { get; private set; }

    public string? Title { get; private set; }

    public string? Slug { get; private set; }

    public static EventGuideReference Create(Guid guideId, string? title, string? slug) =>
        new(
            guideId,
            Clip(title, CommunityLifeLimits.MaxDisplayName),
            Clip(slug, CommunityLifeLimits.MaxSlug));

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
