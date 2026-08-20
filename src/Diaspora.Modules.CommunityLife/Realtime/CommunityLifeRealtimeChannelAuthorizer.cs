using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Realtime;

/// <summary>
/// Who may listen to an organization, and who may listen to an event.
/// <para>
/// <b>This module contributes an authorisation answer and no transport whatsoever.</b> It names no
/// hub, no notifier and no SignalR type; an architecture test asserts it. What it implements is a
/// kernel seam — <see cref="IRealtimeChannelAuthorizer"/> — which exists precisely so that the module
/// which owns a subject can say who may hear about it without the Communication Platform learning
/// what an organization is, and without this module referencing a platform that registers after it.
/// </para>
/// <para>
/// <b>Two prefixes, and one contributor may claim only one.</b> So there are two classes here rather
/// than one answering both: the registry indexes by prefix, and a single class claiming
/// <c>organization:</c> while quietly also answering for <c>event:</c> would be a channel family with
/// no registered owner — refused by the registry and invisible in the owned-prefix list an operator
/// reads.
/// </para>
/// </summary>
public sealed class OrganizationRealtimeChannelAuthorizer : IRealtimeChannelAuthorizer
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly ILogger<OrganizationRealtimeChannelAuthorizer> _logger;

    public OrganizationRealtimeChannelAuthorizer(
        IOrganizationMembershipRepository memberships,
        ILogger<OrganizationRealtimeChannelAuthorizer> logger)
    {
        _memberships = memberships;
        _logger = logger;
    }

    public string Module => CommunityLifeModuleMetadata.Name;

    public string ChannelPrefix => RealtimeChannels.OrganizationPrefix;

    /// <summary>
    /// A current, active member. Nobody else.
    /// <para>
    /// <b>Not "anybody who can see the page".</b> A public organization is readable by the world, and
    /// a live channel is a different thing from a readable record: it says who is watching, it stays
    /// open, and what travels on it — a membership decision, a role change, a notice aimed at
    /// members — is not what a passer-by reads. Requested and Invited are refused too: somebody who
    /// has asked to join has not joined, and a channel that admitted them would announce the
    /// organization's internal traffic to anybody willing to press a button.
    /// </para>
    /// </summary>
    public async Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default)
    {
        if (userId is null
            || RealtimeChannels.IdentifierOf(channel, RealtimeChannels.OrganizationPrefix)
                is not { } parsed)
        {
            return false;
        }

        try
        {
            var membership = await _memberships.FindCurrentAsync(
                OrganizationId.From(parsed), userId, cancellationToken);

            return membership is not null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An authorisation question answers "no" on failure.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not decide whether a member may join an organization channel. Refusing.");

            return false;
        }
#pragma warning restore CA1031
    }

    // ListSubscribersAsync is deliberately left at the seam's default, which names nobody.
    //
    // An organization's membership has no upper bound, and B12 has always refused to let one
    // generate a message per member: "an organization with forty thousand members must not be able
    // to generate forty thousand messages". A bounded page of them would be worse than nothing —
    // whichever forty thousandth the query happened to order first would be told, and the rest would
    // not, with no way for anybody to tell which had happened. An organization-wide announcement is
    // a real capability and it needs a digest and a delivery budget, not a loop over a page.
}

/// <summary>
/// Who may listen to one event: the organizer, and whoever said they were coming.
/// <para>
/// <b>An RSVP is the subscription.</b> The frontend's requirement is that a cancellation or a
/// reschedule reaches the people it inconveniences, and those are exactly the members holding a live
/// response. Somebody browsing a public event needs no socket — they will see the change the next
/// time they look, which is what a refetch is for.
/// </para>
/// <para>
/// <b>The occurrence and the series share this channel.</b> B12.1 settled that an occurrence
/// <em>is</em> an event (ADR-021), so a member who answered for one date is watching that date's
/// record, and a second prefix would be a second name for one subject.
/// </para>
/// </summary>
public sealed class CommunityEventRealtimeChannelAuthorizer : IRealtimeChannelAuthorizer
{
    /// <summary>
    /// The most attendees one fan-out may name.
    /// <para>
    /// Bounded because the seam says so, and set where it is because an event with more live
    /// responses than this is a stadium rather than a gathering — and a stadium's cancellation is an
    /// announcement, which is a different capability with a different delivery budget. The number is
    /// a backstop that no ordinary event reaches, not a page size anybody is expected to iterate.
    /// </para>
    /// </summary>
    public const int MaxAttendeesPerFanOut = 2_000;

    private readonly IEventRsvpRepository _rsvps;
    private readonly ICommunityEventRepository _events;
    private readonly ILogger<CommunityEventRealtimeChannelAuthorizer> _logger;

    public CommunityEventRealtimeChannelAuthorizer(
        IEventRsvpRepository rsvps,
        ICommunityEventRepository events,
        ILogger<CommunityEventRealtimeChannelAuthorizer> logger)
    {
        _rsvps = rsvps;
        _events = events;
        _logger = logger;
    }

    public string Module => CommunityLifeModuleMetadata.Name;

    public string ChannelPrefix => RealtimeChannels.CommunityEventPrefix;

    public async Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default)
    {
        if (userId is null
            || RealtimeChannels.IdentifierOf(channel, RealtimeChannels.CommunityEventPrefix)
                is not { } parsed)
        {
            return false;
        }

        var eventId = CommunityEventId.From(parsed);

        try
        {
            // The response first: it is the common case and a single indexed seek, where reading the
            // event back loads an aggregate with its agenda, speakers and media on it.
            var rsvp = await _rsvps.FindAsync(eventId, userId, cancellationToken);

            if (rsvp is { IsTerminal: false })
            {
                return true;
            }

            var communityEvent = await _events.GetAsync(eventId, cancellationToken);

            return communityEvent is not null && communityEvent.OrganizerUserId == userId;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An authorisation question answers "no" on failure.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Could not decide whether a member may join an event channel. Refusing.");

            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Everybody who should hear about a change to the event, capped.
    /// <para>
    /// <b>Not "everybody currently holding a place", and the difference is the defect this method was
    /// written wrong once already.</b> Cancelling an event releases every response inside the same
    /// transaction, so a list of live responses read afterwards is empty — and a cancellation would
    /// reach nobody, silently, which is the exact failure the frontend register asked to have fixed.
    /// The repository answers the addressable set instead: still holding a place, or released
    /// <em>because</em> the event was called off. A member who withdrew last month is in neither.
    /// </para>
    /// <para>
    /// <b>The organizer is not in this list.</b> They are told about their own event through the
    /// organizer-audience rules the Communication Platform already runs, and adding them here would
    /// send an organizer who cancelled their own event a message telling them it was cancelled.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<UserId>> ListSubscribersAsync(
        string channel, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0
            || RealtimeChannels.IdentifierOf(channel, RealtimeChannels.CommunityEventPrefix)
                is not { } parsed)
        {
            return [];
        }

        var capped = Math.Min(limit, MaxAttendeesPerFanOut);

        try
        {
            var addressable = await _rsvps.FindAddressableAsync(
                CommunityEventId.From(parsed), capped, cancellationToken);

            return [.. addressable.Select(rsvp => rsvp.AttendeeUserId).Distinct()];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A fan-out that cannot be resolved reaches nobody.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Could not list an event's attendees. Nobody was addressed.");

            return [];
        }
#pragma warning restore CA1031
    }
}
