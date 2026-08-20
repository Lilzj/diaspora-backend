namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Why a member reported a piece of content.
/// <para>
/// <b>This enum resolves a contradiction the client has with itself.</b> Its moderation types
/// declare eight PascalCase reason codes; its report modal renders five lower-case category
/// keys, and the modal is what actually submits. B6 takes the eight as the catalogue and maps
/// the five: conflicting maps to <see cref="Misinformation"/> and carries its link as evidence,
/// inaccurate maps to <see cref="Outdated"/>, spam to <see cref="Spam"/>, abusive to
/// <see cref="Harassment"/>, other to <see cref="Other"/>.
/// </para>
/// <para>
/// The eight are the catalogue rather than the five because they are the taxonomy a moderator
/// queue can act on: Copyright and PersonalData carry legal obligations the modal collapses
/// into other, and a report queue that cannot distinguish a takedown request from a grumble is
/// a compliance problem rather than a UX one.
/// </para>
/// </summary>
public enum ReportReasonCode
{
    /// <summary>False or misleading. The modal conflicting category maps here.</summary>
    Misinformation = 0,

    Spam = 1,

    Harassment = 2,

    Inappropriate = 3,

    /// <summary>Was true, no longer is. The modal inaccurate category maps here.</summary>
    Outdated = 4,

    Copyright = 5,

    /// <summary>Exposes someone personal data. Carries a legal clock.</summary>
    PersonalData = 6,

    Other = 7
}
