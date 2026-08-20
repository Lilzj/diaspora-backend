namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Where an offer sits.
/// <para>
/// <see cref="Draft"/> is genuinely useful: terms usually need an internal approval before they
/// go out, and an offer that exists but has not been extended is exactly that state. Nothing is
/// visible to the candidate until <see cref="Extended"/>.
/// </para>
/// <para>
/// <see cref="Expired"/> is reached by the clock rather than by a person, which is why the
/// aggregate takes the current instant on every read of its own status rather than trusting a
/// stored flag.
/// </para>
/// </summary>
public enum OfferStatus
{
    /// <summary>Drafted internally. The candidate cannot see it.</summary>
    Draft = 0,

    /// <summary>With the candidate, awaiting a decision.</summary>
    Extended = 1,

    Accepted = 2,

    Rejected = 3,

    /// <summary>Pulled by the employer before the candidate answered.</summary>
    Withdrawn = 4,

    /// <summary>The response deadline passed unanswered.</summary>
    Expired = 5
}
