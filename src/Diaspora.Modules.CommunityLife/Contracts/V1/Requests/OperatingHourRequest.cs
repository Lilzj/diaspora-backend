namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>One row of an organization's opening hours.</summary>
public sealed record OperatingHourRequest
{
    /// <summary>
    /// <c>Monday</c> to <c>Sunday</c>, or <c>PublicHoliday</c> — which is why this is not
    /// <c>System.DayOfWeek</c>.
    /// </summary>
    public required string Day { get; init; }

    /// <summary>Local time, <c>HH:mm</c>. Omit both times with a note for "by arrangement".</summary>
    public string? OpensAt { get; init; }

    public string? ClosesAt { get; init; }

    /// <summary>Explicitly shut. Different from having no row for the day.</summary>
    public bool IsClosed { get; init; }

    /// <summary>The qualification — "Jumu'ah 13:15", "second Sunday only". Where the real answer
    /// usually lives.</summary>
    public string? Note { get; init; }
}
