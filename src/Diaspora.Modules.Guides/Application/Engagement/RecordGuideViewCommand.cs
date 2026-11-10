using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Records that a reader opened the guide, and how thoroughly they read it.
/// <para>
/// <b>Deliberately not authorized.</b> Anonymous readers are most of the traffic on a knowledge
/// base, and a view counter that only counted signed-in members would report the wrong thing
/// confidently. Uniqueness is the client's claim rather than a server-side fingerprint —
/// honest-enough analytics at the cost the platform is willing to pay for not tracking people.
/// </para>
/// <para>
/// Not <c>[Transactional]</c>, and that is the point: a view bumps counters, and counters do not
/// bump the revision or raise an update event. A reader must never be able to make an author's
/// in-flight save fail.
/// </para>
/// </summary>
[Auditable]
[AllowAnonymous(
    "Deliberately anonymous, and the counter is the reason: readers who share a visa guide with a " +
    "relative are frequently not signed in, and a figure that counted only members would " +
    "understate the one number that measures whether the knowledge base works. Bounded by the " +
    "transport limiter rather than by a token (BSEC-002); BSEC-004 is the finding that asked for " +
    "the bound.")]
public sealed record RecordGuideViewCommand : ICommand
{
    public required Guid GuideId { get; init; }

    public required RecordGuideViewRequest Request { get; init; }
}
