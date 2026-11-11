using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Copies an event the caller organised into a new draft — BG-07.
/// <para>
/// The use case is the one that makes duplication matter most on this platform: a community event
/// that runs again. A monthly meet-up, a weekly language exchange, a term's worth of the same
/// workshop — each is the previous one with a new date.
/// </para>
/// </summary>
[RequirePolicy(CommunityLifePolicies.EventCreate)]
public sealed record DuplicateEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    /// <summary>An optional title for the copy. Null takes the source's, prefixed.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// When the copy should start. Null shifts the source's schedule forward in whole weeks until
    /// it is in the future — see the handler for why whole weeks rather than a fixed offset.
    /// </summary>
    public DateTimeOffset? StartsAt { get; init; }
}
