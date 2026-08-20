namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// The coarse cost band shown on a neighbourhood hub.
/// <para>
/// Three bands because the frontend renders three — <c>'Low' | 'Medium' | 'High'</c> — and a
/// finer scale would imply a precision no data source behind it currently supports. The
/// value is nullable everywhere it appears: see known gap #1 in the frontend contract report.
/// </para>
/// </summary>
public enum CostOfLivingBand
{
    Low = 0,
    Medium = 1,
    High = 2
}
