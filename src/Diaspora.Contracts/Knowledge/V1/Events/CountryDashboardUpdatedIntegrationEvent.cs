using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Knowledge.V1.Events;

/// <summary>
/// The numbers behind one country's dashboard moved.
/// <para>
/// <b>Distinct from <see cref="CountryKnowledgeDirectoryUpdatedIntegrationEvent"/>, and the
/// distinction is who caused it.</b> A directory update is an editorial act — somebody arranged
/// the page. A dashboard update is arithmetic: a guide was published, a job posted, a property
/// listed, and the country's counters are no longer what a cached dashboard says they are. The two
/// have different rates, different audiences and different staleness costs, so folding them into
/// one event would make every consumer of either subscribe to both.
/// </para>
/// <para>
/// Emitted from the contextual-summary projection, which is maintained from other verticals'
/// <em>published contracts</em> — never from their modules. See the cross-module integration
/// guide.
/// </para>
/// </summary>
public sealed record CountryDashboardUpdatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// Which counter moved — <c>guides</c>, <c>jobs</c>, <c>housing</c>, <c>marketplace</c>,
    /// <c>events</c>, <c>organizations</c>, <c>providers</c>. Open-ended by design: a later
    /// vertical contributes a counter without this contract changing.
    /// </summary>
    public required string Counter { get; init; }

    /// <summary>The counter's value after the change.</summary>
    public required int Count { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
