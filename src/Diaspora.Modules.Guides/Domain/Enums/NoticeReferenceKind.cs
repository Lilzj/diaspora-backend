namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// What kind of thing a notice points at.
/// <para>
/// <b>One collection of typed references rather than eight collections, which is B6's
/// <c>ContentReference</c> lesson applied to a different problem.</b> The brief lists guides,
/// learning paths, organizations, events, providers, jobs, housing and marketplace listings as
/// things a notice may reference. Eight owned collections would be eight tables, eight
/// configurations, eight limit checks and eight places to forget one — for a set that is only ever
/// read as "what else should I look at".
/// </para>
/// <para>
/// <b>A reference is an identifier and a kind, never a copy.</b> No title, no summary, no status:
/// the owning module answers those at read time with its own visibility rules. A notice that cached
/// a job title would keep showing it after the job closed.
/// </para>
/// </summary>
public enum NoticeReferenceKind
{
    /// <summary>A guide. The commonest reference by far — "here is how to do it".</summary>
    Guide = 0,

    /// <summary>A curated onboarding route from B8.1.</summary>
    LearningPath = 1,

    /// <summary>A community organization from B12.</summary>
    Organization = 2,

    /// <summary>An event from B12 — an outreach day, a mobile consulate.</summary>
    Event = 3,

    /// <summary>A professional from B7 — an immigration adviser, a translator.</summary>
    Provider = 4,

    /// <summary>A job posting from B9.</summary>
    Job = 5,

    /// <summary>A housing listing from B11.</summary>
    Housing = 6,

    /// <summary>A marketplace listing from B10.</summary>
    Listing = 7,

    /// <summary>A mission in this module's own embassy directory.</summary>
    Embassy = 8
}
