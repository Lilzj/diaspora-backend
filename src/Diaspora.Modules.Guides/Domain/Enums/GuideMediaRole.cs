namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// What job a referenced media asset does on the guide. The pair (media, role) is the
/// idempotency key for attachment — the same image may legitimately be both the hero and a
/// gallery item.
/// </summary>
public enum GuideMediaRole
{
    Hero = 0,
    Cover = 1,
    Gallery = 2,
    Attachment = 3,
    Embedded = 4
}
