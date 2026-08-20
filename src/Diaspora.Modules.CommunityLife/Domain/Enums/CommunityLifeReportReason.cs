namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Why somebody reported an event or an organization.
/// <para>
/// <see cref="Duplicate"/> is the one the brief names that is not abuse, and it earns its place:
/// the same festival posted four times by four committee members is the single most common
/// complaint about any community calendar, and it needs an answer that merges rather than bans.
/// </para>
/// </summary>
public enum CommunityLifeReportReason
{
    /// <summary>Advertising dressed as a community event.</summary>
    Spam = 0,

    /// <summary>The same thing, already posted.</summary>
    Duplicate = 1,

    /// <summary>Not real. The organizer is collecting something and giving nothing.</summary>
    Fraud = 2,

    /// <summary>Pretending to be an organization it is not.</summary>
    Impersonation = 3,

    /// <summary>Content that does not belong on the platform.</summary>
    InappropriateContent = 4,

    /// <summary>Targets a group.</summary>
    HateSpeech = 5,

    /// <summary>The details are wrong — the time, the place, the price.</summary>
    Misinformation = 6,

    /// <summary>Called off, and still advertised.</summary>
    EventDidNotHappen = 7,

    /// <summary>Something else. The moderator reads the note.</summary>
    Other = 99
}
