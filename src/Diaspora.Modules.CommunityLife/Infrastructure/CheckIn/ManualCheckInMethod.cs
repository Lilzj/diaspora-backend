using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Extensibility.CheckIn;

namespace Diaspora.Modules.CommunityLife.Infrastructure.CheckIn;

/// <summary>
/// The shipped check-in method: somebody on the door ticks a name off a list.
/// <para>
/// <b>The one that must always work.</b> Every other method — a scanned code, a self-confirmation —
/// depends on a phone, a signal or a printer, and at a community hall on a Saturday one of those is
/// usually missing. This resolves from the identifiers the caller already has, needs nothing, and is
/// what the dispatcher falls through to when a scanner fails mid-queue.
/// </para>
/// <para>
/// It resolves rather than verifies: whether that response may be checked in at all is the
/// aggregate's decision, and whether the caller may work the door is the authorization service's.
/// </para>
/// </summary>
public sealed class ManualCheckInMethod : IEventCheckInMethod
{
    public CheckInMethod Method => CheckInMethod.Manual;

    public bool CanHandle(CheckInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RsvpId is { } rsvpId && rsvpId != Guid.Empty
            || (request.AttendeeUserId is { } attendeeId && attendeeId != Guid.Empty);
    }

    public Task<CheckInOutcome> ResolveAsync(
        CheckInRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RsvpId is { } rsvpId && rsvpId != Guid.Empty)
        {
            // The attendee is unknown from a response identifier alone, and the handler does not
            // need it — it loads the response and reads the attendee off that.
            return Task.FromResult(
                CheckInOutcome.Resolved(rsvpId, request.AttendeeUserId ?? Guid.Empty));
        }

        return Task.FromResult(
            request.AttendeeUserId is { } attendeeId && attendeeId != Guid.Empty
                ? CheckInOutcome.Resolved(Guid.Empty, attendeeId)
                : CheckInOutcome.Unresolved("No attendee or response was named."));
    }
}
