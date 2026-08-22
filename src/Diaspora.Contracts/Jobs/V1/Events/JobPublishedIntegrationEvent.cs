using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A job posting became visible to candidates.
/// <para>
/// <b>The most consequential contract this module publishes.</b> It is what puts a vacancy into
/// a search index, fires every matching standing alert, and starts the clock an employer's
/// response-time reputation is measured against.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> is how a consumer tells "a new job exists" from "an existing
/// job was corrected": a notification fan-out wants the first only — a candidate re-notified
/// because the employer fixed a typo learns to ignore the notifications — while a search index
/// wants them all.
/// </para>
/// <para>
/// The payload carries everything a card-level index needs, including the two fields that decide
/// whether a diaspora candidate can act on it at all: <see cref="WorkplaceType"/> and
/// <see cref="OffersVisaSponsorship"/>. Most consumers never call back into the Jobs module.
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record JobPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    /// <summary>The employer's trading name, denormalised so a card needs no second read.</summary>
    public required string EmployerName { get; init; }

    /// <summary>Whether the employer carried a Trust mark at publication.</summary>
    public required bool IsEmployerVerified { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    public string? Summary { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>Normalised tag slugs, in display order.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Normalised skill keys the posting asks for.</summary>
    public required IReadOnlyList<string> SkillKeys { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary><c>OnSite</c>, <c>Hybrid</c> or <c>Remote</c>.</summary>
    public required string WorkplaceType { get; init; }

    /// <summary><c>FullTime</c>, <c>Contract</c>, <c>Internship</c> and so on.</summary>
    public required string JobType { get; init; }

    public required string ExperienceLevel { get; init; }

    /// <summary>The employer's claim that it will sponsor a work visa. Never verified here.</summary>
    public required bool OffersVisaSponsorship { get; init; }

    public required bool OffersRelocationAssistance { get; init; }

    /// <summary>False when the employer published no figure, which is the common case.</summary>
    public required bool IsSalaryDisclosed { get; init; }

    public decimal? SalaryMinimum { get; init; }

    public decimal? SalaryMaximum { get; init; }

    /// <summary>ISO 4217, upper case. Null when nothing was published. Never converted.</summary>
    public string? SalaryCurrency { get; init; }

    /// <summary><c>Yearly</c>, <c>Monthly</c>, <c>Hourly</c> and so on.</summary>
    public string? SalaryPeriod { get; init; }

    /// <summary>When applications stop. Null means open until the employer closes it.</summary>
    public DateTimeOffset? ClosingAt { get; init; }

    public required int VacancyCount { get; init; }

    /// <summary>False on a republication. Standing alerts fire on the first only.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// The member behind the employer profile.
    /// <para>
    /// <b>The field an attribution consumer cannot do its job without.</b> This contract named only
    /// <see cref="EmployerId"/> — the <em>organisation</em> — so a consumer that wanted to credit
    /// the person who posted the vacancy had a profile identifier and no way to reach them.
    /// <c>ListingPublishedIntegrationEvent.SellerUserId</c> was added for exactly this reason in
    /// Wave 12 and states it in the same words; this is the same field on the same argument, and its
    /// absence here is BG-10.
    /// </para>
    /// <para>
    /// <b>Attribution, not delivery.</b> It is <c>EmployerProfile.OwnerUserId</c> — the member who
    /// registered the organisation — and deliberately not <c>NotifiableOwnerUserId</c>, which
    /// follows an ownership transfer. Who published a vacancy in March does not change because the
    /// company changed hands in June, and a credit that moved with the transfer would restate
    /// history.
    /// </para>
    /// <para>
    /// <b>Costs nothing to publish.</b> The relay already reads the employer aggregate back to fill
    /// in <see cref="EmployerName"/> and <see cref="IsEmployerVerified"/>, so there is no new read.
    /// </para>
    /// <para>
    /// <b>Appended, never inserted.</b> Property order is part of the serialised shape, and a field
    /// added in the middle changes how every previously-written outbox row deserialises. Null on
    /// events published before this change, and null when the employer could not be read.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
