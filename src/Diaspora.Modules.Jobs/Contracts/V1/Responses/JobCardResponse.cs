namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One job in a list — search results, browse, saved, recommendations.
/// <para>
/// <b>Deliberately much smaller than the detail response.</b> A results page of twenty cards that
/// carried twenty full adverts would be several hundred kilobytes to render twenty titles, on a
/// connection this platform's members frequently pay for by the megabyte.
/// </para>
/// <para>
/// The three fields a general job board would omit are here on purpose:
/// <see cref="OffersVisaSponsorship"/>, <see cref="WorkplaceType"/> and
/// <see cref="IsEmployerVerified"/>. They are what a diaspora candidate scans a list for, and
/// making them open the detail page to find out is what makes a job board exhausting to use.
/// </para>
/// </summary>
public sealed record JobCardResponse
{
    public required Guid JobId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    public Guid? EmployerLogoMediaId { get; init; }

    public required bool IsEmployerVerified { get; init; }

    /// <summary>Whether the employer is an agency rather than the hirer. Candidates should know.</summary>
    public required bool IsIntermediary { get; init; }

    public required string CategoryKey { get; init; }

    public required string JobType { get; init; }

    public required string ExperienceLevel { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>The most specific place the posting named. What the card actually prints.</summary>
    public required string LocationLabel { get; init; }

    public required string WorkplaceType { get; init; }

    public required bool IsRemote { get; init; }

    public required bool OffersVisaSponsorship { get; init; }

    public required bool OffersRelocationAssistance { get; init; }

    public required JobSalaryResponse Salary { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>The top few skill names, for chips. Not the whole list.</summary>
    public required IReadOnlyList<string> Skills { get; init; }

    public required string Status { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsVerified { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ClosingAt { get; init; }

    public required int ApplicationCount { get; init; }

    /// <summary>Whether this viewer has applied. Null when anonymous.</summary>
    public bool? HasApplied { get; init; }

    public bool? IsSaved { get; init; }

    /// <summary>Why this was recommended, on a recommendations surface. Null elsewhere.</summary>
    public string? RecommendationReason { get; init; }
}
