using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised the moment an account row is created, before phone verification.</summary>
public sealed record UserRegisteredDomainEvent(
    UserId UserId,
    string PhoneNumber,
    string FullName,
    string Nationality,
    string CountryOfResidence,
    string PreferredLanguage) : DomainEvent;
