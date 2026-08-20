using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;

namespace Diaspora.Modules.Guides.Application.Knowledge.Analytics;

/// <summary>
/// Records something a reader did with the directory that the server could not otherwise see.
/// <para>
/// <b>Client-reported because it has to be.</b> Opening a country page is a request the server
/// observes. Tapping through to a guide from a shelf, or reaching the end of one, is not — the guide
/// read is a request to a different endpoint that carries no memory of where the reader came from.
/// Without this the directory could count arrivals and never learn whether any of them led anywhere,
/// which is the one number that says whether it works.
/// </para>
/// <para>
/// <b>Unauthenticated, and it accepts no reader identity.</b> The counters this feeds are aggregate
/// totals per country and per category. A directory that recorded which member read about
/// immigration would be holding something it has no business holding — so the contract has nowhere
/// to put one.
/// </para>
/// <para>
/// Deliberately not <c>[Auditable]</c>. An audit row per view would be an audit log the size of the
/// traffic, which makes the actual audit trail — who curated what — impossible to find.
/// </para>
/// </summary>
public sealed record RecordKnowledgeActivityCommand : ICommand
{
    public required string CountryCode { get; init; }

    /// <summary>One of the constants in <c>KnowledgeAnalyticsSignalKinds</c>.</summary>
    public required string Kind { get; init; }

    public required RecordKnowledgeActivityRequest Request { get; init; }
}
