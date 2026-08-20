using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when a suspension or hard lock is lifted.</summary>
public sealed record UserAccountReinstatedDomainEvent(
    UserId UserId,
    string RestoredState,
    DateTimeOffset ReinstatedAt) : DomainEvent;
