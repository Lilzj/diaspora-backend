namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Filters for the operator-facing listings.
/// <para>
/// Every field is optional and <see cref="Limit"/> is capped by the store. An administration
/// endpoint reading an unbounded outbox table during an incident is the classic way to turn a
/// delivery backlog into a database outage.
/// </para>
/// </summary>
/// <param name="Module">Restrict to one producing module.</param>
/// <param name="EventType">Restrict to one contract name.</param>
/// <param name="TenantId">Restrict to one tenant.</param>
/// <param name="CorrelationId">Restrict to one request's trail.</param>
/// <param name="MinAttempts">Only messages that have failed at least this many times.</param>
/// <param name="Limit">Maximum rows to return.</param>
/// <param name="Offset">Rows to skip, for paging through a backlog.</param>
public sealed record OutboxQuery(
    string? Module = null,
    string? EventType = null,
    string? TenantId = null,
    string? CorrelationId = null,
    int? MinAttempts = null,
    int Limit = 50,
    int Offset = 0);
