using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a posting becomes visible to candidates.
/// <para>
/// <b>The most consequential event in the module.</b> It is what puts a job into the search
/// index, what fires every matching standing alert, and what starts the clock the employer's
/// response-time figure is measured against. It is raised alongside the ordinary status change
/// rather than instead of it, because a consumer watching lifecycle transitions and a consumer
/// watching for new vacancies want different things and should not have to filter.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> is what distinguishes a new vacancy from a re-publication
/// after an edit. Alerts fire on the first only — a candidate notified again because the
/// employer fixed a typo learns to ignore the notifications.
/// </para>
/// </summary>
public sealed record JobPostingPublishedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public UserId? PublishedBy { get; init; }

    /// <summary>False on a re-publication. Standing alerts fire on the first only.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
