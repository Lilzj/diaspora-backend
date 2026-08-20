namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Who a published guide is shown to. Unlisted exists for the share-a-link workflow — a guide
/// an author wants reviewed by a friend before it enters discovery.
/// </summary>
public enum GuideVisibility
{
    Public = 0,
    Unlisted = 1,
    Private = 2
}
