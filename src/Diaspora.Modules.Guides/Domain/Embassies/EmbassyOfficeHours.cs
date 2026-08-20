using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Embassies;

/// <summary>
/// When a mission is open on one day of the week.
/// <para>
/// <b>Free text rather than a pair of times, and that is a decision rather than laziness.</b> Real
/// missions publish "09:00–12:00 and 14:00–16:00, closed for visa collection after 11:00", "by
/// appointment only", and "08:30–15:30 during Ramadan". A structured open/close pair can express
/// none of those, so a structured model would either force the editor to lie or grow a free-text
/// override beside it — at which point the structure has become a second field nobody trusts.
/// </para>
/// <para>
/// <see cref="IsClosed"/> is the one thing that <em>is</em> structured, because it is the one thing a
/// client renders differently: a greyed-out row rather than a line of text. Everything else is
/// transcribed from the mission's own page and shown as written.
/// </para>
/// </summary>
public sealed class EmbassyOfficeHours
{
    private EmbassyOfficeHours(DayOfWeek day, string? hours, bool isClosed)
    {
        Day = day;
        Hours = hours;
        IsClosed = isClosed;
    }

    private EmbassyOfficeHours() { }

    /// <summary>The day. The row's identity within its mission.</summary>
    public DayOfWeek Day { get; private set; }

    /// <summary>What the mission says about that day, verbatim.</summary>
    public string? Hours { get; private set; }

    /// <summary>Whether the mission is shut. Rendered as a state, not as a sentence.</summary>
    public bool IsClosed { get; private set; }

    internal static EmbassyOfficeHours? TryCreate(DayOfWeek day, string? hours, bool isClosed)
    {
        if (!Enum.IsDefined(day))
        {
            return null;
        }

        var text = IntelligenceNormalization.Text(hours, IntelligenceLimits.MaxOfficeHours);

        // A row that says nothing and is not a closure is a row with no content. Refusing it keeps
        // an "opening hours" panel from rendering four blank lines.
        return text is null && !isClosed
            ? null
            : new EmbassyOfficeHours(day, text, isClosed);
    }
}
