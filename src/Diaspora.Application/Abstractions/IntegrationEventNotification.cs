using Diaspora.Contracts.Events;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Wraps an integration event so MediatR can publish it to in-process consumers.
/// <para>
/// The twin of <see cref="DomainEventNotification{T}"/>, and separate from it on purpose.
/// A single wrapper constrained to <c>IMessage</c> would compile and would quietly restore the
/// defect B1.2 exists to remove: a handler written against the domain pipeline would once again
/// be reachable by a published contract, and nothing would say which pipeline a given
/// notification came from. Two wrappers means
/// <c>DomainEventNotification&lt;SomeIntegrationEvent&gt;</c> does not typecheck.
/// </para>
/// <para>
/// <c>Diaspora.Contracts</c> takes no framework dependency — a consumer in another process must
/// be able to parse a contract without acquiring a mediator — so the wrapping happens here, at
/// the transport boundary, exactly as it does for domain events.
/// </para>
/// </summary>
public sealed record IntegrationEventNotification<TIntegrationEvent>(TIntegrationEvent IntegrationEvent)
    : INotification
    where TIntegrationEvent : IIntegrationEvent;
