using Diaspora.SharedKernel.Primitives;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Wraps a domain event so MediatR can publish it.
/// <para>
/// The Domain project has zero framework dependencies, so its events cannot implement
/// <c>INotification</c> directly. Wrapping at the dispatch boundary keeps that promise
/// intact while still giving handlers strongly typed events.
/// </para>
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
