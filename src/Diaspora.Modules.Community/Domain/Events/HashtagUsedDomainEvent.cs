using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>Raised each time a hashtag is attached to content. Feeds trending and search.</summary>
public sealed record HashtagUsedDomainEvent : DomainEvent
{
    public required HashtagId HashtagId { get; init; }

    public required string Slug { get; init; }

    public required ContentReference Content { get; init; }

    public string? CountryCode { get; init; }
}
