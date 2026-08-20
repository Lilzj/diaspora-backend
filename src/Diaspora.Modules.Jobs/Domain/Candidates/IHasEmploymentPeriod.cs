using Diaspora.Modules.Jobs.Domain.ValueObjects;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// Implemented by the candidate entries that span a period of time — a position held, a course
/// studied.
/// <para>
/// <b>It exists for the persistence layer, and that is worth being honest about.</b> Nothing in
/// the domain dispatches on it: the two implementers are unrelated concepts that happen to share
/// a value object. What it buys is one strongly-typed <c>ConfigurePeriod</c> in the EF
/// configuration instead of two copies that would eventually differ in a column name and
/// produce experience and education rows that no longer compare.
/// </para>
/// </summary>
public interface IHasEmploymentPeriod
{
    /// <summary>When the entry started and, if it has, ended.</summary>
    EmploymentPeriod Period { get; }
}
