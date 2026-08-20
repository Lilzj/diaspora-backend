namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// How often a series repeats.
/// <para>
/// <b>Four frequencies, and deliberately not RFC 5545 in full.</b> The standard's grammar carries
/// <c>BYSETPOS</c>, <c>BYYEARDAY</c>, <c>BYWEEKNO</c>, workweek-start sensitivity and a dozen more
/// dimensions, none of which any screen in this product asks for and every one of which is a
/// separate expansion path to keep correct. What a diaspora community actually runs is a weekly
/// service, a fortnightly meetup, a monthly committee and an annual festival — daily, weekly,
/// monthly, yearly — and the rule is shaped after the standard's field names so a later phase can
/// widen it without renaming a contract.
/// </para>
/// <para>
/// <b>The client's <c>Custom</c> is not a fifth frequency.</b> It means "weekly, on these weekdays",
/// which is <see cref="Weekly"/> with a <c>byWeekday</c> list, and the server normalises it to that
/// rather than storing a word with no expansion semantics behind it. The normalised rule and the
/// server's own description both travel back on the response, so the editor sees what was stored.
/// </para>
/// </summary>
public enum RecurrenceFrequency
{
    /// <summary>Every N days.</summary>
    Daily = 0,

    /// <summary>Every N weeks, on the named weekdays, or on the start date's weekday.</summary>
    Weekly = 1,

    /// <summary>Every N months, on the named days of the month, or on the start date's day.</summary>
    Monthly = 2,

    /// <summary>Every N years, on the start date's month and day.</summary>
    Yearly = 3
}
