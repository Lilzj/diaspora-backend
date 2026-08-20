using Diaspora.Platform.Messaging.Outbox.Options;

namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// One worker's request for work.
/// </summary>
/// <param name="ClaimId">
/// Identifies this claim. The store stamps it onto the rows its conditional update won, then
/// reads back exactly those rows — which is how a batch is claimed atomically on any provider,
/// without holding a transaction open across a read and without <c>SKIP LOCKED</c>, which
/// SQLite does not have and the integration suite therefore could not exercise.
/// </param>
/// <param name="WorkerId">Who is claiming, for diagnostics.</param>
/// <param name="BatchSize">Maximum messages to take.</param>
/// <param name="Now">The clock reading this pass is evaluated against.</param>
/// <param name="LeaseExpiresAt">When the claim lapses if the worker does not finish.</param>
/// <param name="Ordering">Which ordering guarantee the claim must respect.</param>
/// <param name="TenantIds">Tenants to restrict to; empty means every tenant.</param>
public sealed record OutboxClaimRequest(
    Guid ClaimId,
    string WorkerId,
    int BatchSize,
    DateTimeOffset Now,
    DateTimeOffset LeaseExpiresAt,
    OutboxOrderingMode Ordering,
    IReadOnlyCollection<string> TenantIds);
