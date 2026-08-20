using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A published job posting changed.
/// <para>
/// One contract for every kind of edit, distinguished by <see cref="ChangedFields"/>. To the
/// consumers that matter — a search index, a cache, an analytics rollup — the fact is identical:
/// this posting changed, re-read it. Splitting it by field would make every consumer subscribe to
/// a dozen contracts to learn one thing.
/// </para>
/// <para>
/// The field names are stable strings, so a consumer can cheaply ignore a change it does not care
/// about: a salary or closing-date edit matters to a standing alert, a tag edit does not.
/// </para>
/// </summary>
public sealed record JobUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public Guid? ChangedBy { get; init; }

    /// <summary>
    /// Which parts moved: <c>title</c>, <c>description</c>, <c>salary</c>, <c>location</c>,
    /// <c>workplace</c>, <c>skills</c>, <c>tags</c>, <c>category</c>, <c>closing-date</c>,
    /// <c>vacancies</c>, <c>visibility</c>.
    /// </summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
