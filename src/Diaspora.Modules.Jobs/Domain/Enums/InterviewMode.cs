namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// How an interview is conducted.
/// <para>
/// The mode decides which detail is required: a remote mode needs a joining link and an on-site
/// mode needs an address, and <c>InterviewSchedule</c> enforces exactly that. A cross-border
/// candidate told only "interview on Tuesday" cannot tell whether they need a visa appointment.
/// </para>
/// <para>
/// <b>The platform hosts none of these.</b> Video interviewing is explicitly out of scope for
/// B9; <see cref="Video"/> means the employer supplies a link to whatever they already use.
/// </para>
/// </summary>
public enum InterviewMode
{
    Phone = 1,

    /// <summary>Conducted over a video call the employer hosts elsewhere.</summary>
    Video = 2,

    /// <summary>At the employer's premises. Requires an address.</summary>
    OnSite = 3,

    /// <summary>Several interviewers at once.</summary>
    Panel = 4,

    /// <summary>A technical exercise conducted live.</summary>
    Technical = 5,

    /// <summary>A take-home or timed assessment. Not attended.</summary>
    Assessment = 6
}
