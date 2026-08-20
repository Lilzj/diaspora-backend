using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.Series;

/// <summary>
/// A date the rule generates and the series no longer holds.
/// <para>
/// <b>A date, not an occurrence identifier, and that is what makes it an exception to the
/// <em>rule</em>.</b> The generator asks "does the rule still own this date"; an exception keyed on
/// an occurrence would answer a different question and would be silently wrong the moment the rule
/// was re-expanded — the cancelled week would come straight back as a new occurrence with a new
/// identifier, and everybody who had been told it was off would be re-invited to it.
/// </para>
/// <para>
/// Two things produce one: cancelling a single occurrence, and moving one off its generated date.
/// <see cref="Reason"/> distinguishes them, so a series page can say which without loading the
/// occurrence.
/// </para>
/// </summary>
public sealed class SeriesDateException
{
    private SeriesDateException(DateOnly localDate, string reason, DateTimeOffset createdAt)
    {
        LocalDate = localDate;
        Reason = reason;
        CreatedAt = createdAt;
    }

    private SeriesDateException() { }

    /// <summary>The local date in the series' own zone. The rule's own vocabulary.</summary>
    public DateOnly LocalDate { get; private set; }

    /// <summary>One of <see cref="SeriesExceptionReason"/>.</summary>
    public string Reason { get; private set; } = SeriesExceptionReason.Cancelled;

    public DateTimeOffset CreatedAt { get; private set; }

    public static SeriesDateException Create(
        DateOnly localDate, string? reason, DateTimeOffset createdAt) =>
        new(
            localDate,
            Clip(reason, CommunityLifeLimits.MaxExceptionReason),
            createdAt);

    private static string Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SeriesExceptionReason.Cancelled;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}

/// <summary>Why a generated date is no longer part of the series.</summary>
public static class SeriesExceptionReason
{
    /// <summary>The occurrence on that date was called off.</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>The occurrence was moved to a different date and no longer sits on this one.</summary>
    public const string Moved = "Moved";

    /// <summary>The organizer removed the date before anything was generated for it.</summary>
    public const string Skipped = "Skipped";
}
