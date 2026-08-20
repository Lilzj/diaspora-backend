namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Whether a hashtag may be surfaced.
/// <para>
/// Hashtags are the one part of this module a member can create without any review at all —
/// typing one invents it — which makes them the obvious vector for a slur in a trending list.
/// <see cref="Blocked"/> exists so a tag can be refused everywhere at once, and
/// <see cref="Suppressed"/> so a tag can stay usable while being kept out of trending and
/// autocomplete, which is the response most abuse actually warrants.
/// </para>
/// </summary>
public enum HashtagStatus
{
    Active = 0,

    /// <summary>Usable, but never suggested and never trending.</summary>
    Suppressed = 1,

    /// <summary>Refused on creation and stripped from bodies.</summary>
    Blocked = 2
}
