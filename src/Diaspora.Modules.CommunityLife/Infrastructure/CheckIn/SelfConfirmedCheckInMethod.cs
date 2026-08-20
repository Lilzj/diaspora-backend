using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Extensibility.CheckIn;

namespace Diaspora.Modules.CommunityLife.Infrastructure.CheckIn;

/// <summary>
/// The second shipped method: the attendee says they are here.
/// <para>
/// <b>For the events nobody is standing at a door for.</b> A prayer meeting, a support session or an
/// online workshop has no queue and no volunteer with a list, and an attendance record that only
/// exists where somebody was staffing a door would say those events had nobody at them.
/// </para>
/// <para>
/// <b>Only ever resolves the caller to themselves.</b> The recorder and the attendee have to be the
/// same person — otherwise this would be a way to mark somebody else present at a meeting they never
/// came to, which for a support group or a political association is a record that could do real
/// harm. Somebody checking another person in goes through the manual method, which requires the door
/// permission.
/// </para>
/// </summary>
public sealed class SelfConfirmedCheckInMethod : IEventCheckInMethod
{
    public CheckInMethod Method => CheckInMethod.SelfConfirmed;

    public bool CanHandle(CheckInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RecordedByUserId is { } recorder
            && recorder != Guid.Empty
            && (request.AttendeeUserId is null || request.AttendeeUserId == recorder)
            && request.RsvpId is null;
    }

    public Task<CheckInOutcome> ResolveAsync(
        CheckInRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(
            request.RecordedByUserId is { } recorder && recorder != Guid.Empty
                ? CheckInOutcome.Resolved(Guid.Empty, recorder)
                : CheckInOutcome.Unresolved("Nobody is signed in to confirm."));
    }
}
