namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Headcount, bucketed.
/// <para>
/// Buckets rather than a number, because the exact figure is a fact employers get wrong, update
/// rarely and occasionally treat as confidential — and because no candidate filters on 247. The
/// buckets are the ones every jobs product converges on, so the filter behaves the way people
/// already expect.
/// </para>
/// </summary>
public enum CompanySize
{
    /// <summary>Not stated.</summary>
    Unspecified = 0,

    /// <summary>1–10.</summary>
    Micro = 1,

    /// <summary>11–50.</summary>
    Small = 2,

    /// <summary>51–250.</summary>
    Medium = 3,

    /// <summary>251–1,000.</summary>
    Large = 4,

    /// <summary>1,001–5,000.</summary>
    VeryLarge = 5,

    /// <summary>More than 5,000.</summary>
    Enterprise = 6
}
