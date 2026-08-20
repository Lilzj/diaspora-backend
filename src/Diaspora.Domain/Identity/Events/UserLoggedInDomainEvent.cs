using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised on a successful credential exchange.</summary>
public sealed record UserLoggedInDomainEvent(
    UserId UserId,
    SessionId SessionId,
    string AccessState,
    DateTimeOffset LoggedInAt) : DomainEvent;
