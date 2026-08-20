namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Where a posting sits in its life.
/// <para>
/// <b>Nine states, and the ordering is the brief's.</b> Draft, Published, Open, Receiving
/// Applications, Interviewing, Offer Made, Filled, Expired, Archived. Most job boards collapse
/// the middle five into "open" and lose the only information a candidate actually wants: not
/// whether the advert is still up, but whether anyone is still being considered. A posting that
/// reached <see cref="Interviewing"/> three weeks ago is a very different proposition from one
/// that opened yesterday, and a candidate deciding where to spend an afternoon deserves to know
/// which they are looking at.
/// </para>
/// <para>
/// <b>Published and Open are genuinely different.</b> Published means the posting is visible;
/// Open means it is taking applications. An employer who advertises a role a week before the
/// application window opens is doing something ordinary, and a status machine that cannot say
/// so forces them to either lie or wait.
/// </para>
/// <para>
/// The progression is forward-only through the hiring states, with two exits available from
/// almost anywhere — <see cref="Expired"/> when the closing date passes and
/// <see cref="Archived"/> when the employer retires it. <c>JobStatusTransitions</c> is the
/// single place the legal moves are declared.
/// </para>
/// </summary>
public enum JobStatus
{
    /// <summary>Being written. Visible only to the employer's own team.</summary>
    Draft = 0,

    /// <summary>Live and visible, but not yet accepting applications.</summary>
    Published = 1,

    /// <summary>Open for applications.</summary>
    Open = 2,

    /// <summary>Applications have started arriving. Set by the first application, not by hand.</summary>
    ReceivingApplications = 3,

    /// <summary>Candidates are being interviewed.</summary>
    Interviewing = 4,

    /// <summary>An offer is out with a candidate.</summary>
    OfferMade = 5,

    /// <summary>Every vacancy is taken. The end state a successful posting reaches.</summary>
    Filled = 6,

    /// <summary>The closing date passed. Reached by the clock, not by a person.</summary>
    Expired = 7,

    /// <summary>Retired by the employer. Off every public surface, kept for the record.</summary>
    Archived = 8
}
