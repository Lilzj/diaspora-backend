using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when one seat on a posting is taken.
/// <para>
/// Separate from the status reaching <c>Filled</c>, because a posting advertising six vacancies
/// fills five of them without its status changing at all. The two are only the same event on a
/// single-vacancy posting, which is the common case and exactly why the distinction is easy to
/// miss and worth making explicit.
/// </para>
/// </summary>
public sealed record JobPostingVacancyFilledDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required JobApplicationId ApplicationId { get; init; }

    /// <summary>Seats taken so far, this one included.</summary>
    public required int FilledCount { get; init; }

    /// <summary>Seats the posting advertises in total.</summary>
    public required int VacancyCount { get; init; }

    public required DateTimeOffset FilledAt { get; init; }
}
