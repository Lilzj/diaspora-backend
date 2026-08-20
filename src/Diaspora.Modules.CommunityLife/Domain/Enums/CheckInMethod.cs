namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// How somebody was recorded as present.
/// <para>
/// <b><see cref="QrCode"/> exists and nothing implements it, deliberately.</b> The brief puts QR
/// infrastructure explicitly out of scope while asking that check-in be provider-independent and
/// future-ready. The honest way to be both is to name the method now — so an attendance record
/// written by a scanner in a year is distinguishable from one a volunteer typed — and register no
/// method that produces it. See <c>IEventCheckInMethod</c>.
/// </para>
/// </summary>
public enum CheckInMethod
{
    /// <summary>Somebody on the door found the name and tapped it.</summary>
    Manual = 0,

    /// <summary>The organizer marked the whole list, after the fact.</summary>
    Organizer = 1,

    /// <summary>The attendee confirmed their own arrival.</summary>
    SelfConfirmed = 2,

    /// <summary>Scanned. Reserved; no method registered in this deployment produces it.</summary>
    QrCode = 3
}
