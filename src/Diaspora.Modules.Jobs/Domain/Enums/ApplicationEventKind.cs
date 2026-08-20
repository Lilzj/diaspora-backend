namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What happened on an application, recorded on its append-only history.
/// <para>
/// <b>The history is the point of the aggregate.</b> A status column tells a candidate where
/// they are; the history tells them how long they waited at each step, and tells an employer
/// where their process actually loses people. It is also the evidence in a dispute, which is
/// why entries are appended and never edited or removed.
/// </para>
/// </summary>
public enum ApplicationEventKind
{
    /// <summary>The application was submitted.</summary>
    Submitted = 1,

    /// <summary>The status moved.</summary>
    StatusChanged = 2,

    /// <summary>The application moved between pipeline stages.</summary>
    StageChanged = 3,

    /// <summary>An interview was scheduled against it.</summary>
    InterviewScheduled = 4,

    /// <summary>An interview outcome was recorded.</summary>
    InterviewCompleted = 5,

    /// <summary>Terms were put to the candidate.</summary>
    OfferExtended = 6,

    /// <summary>The candidate answered an offer.</summary>
    OfferAnswered = 7,

    /// <summary>The employer left an internal note.</summary>
    NoteAdded = 8,

    /// <summary>The candidate withdrew.</summary>
    Withdrawn = 9,

    /// <summary>The employer viewed it for the first time.</summary>
    Viewed = 10
}
