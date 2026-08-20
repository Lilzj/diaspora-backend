using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when member-editable profile fields change.</summary>
public sealed record UserProfileUpdatedDomainEvent(
    UserId UserId,
    IReadOnlyCollection<string> ChangedFields,
    DateTimeOffset UpdatedAt) : DomainEvent;
