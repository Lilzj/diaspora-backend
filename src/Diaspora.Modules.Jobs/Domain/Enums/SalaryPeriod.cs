namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// The unit a salary figure is quoted in.
/// <para>
/// <b>Required whenever a figure is present, and the reason is cross-border comparison.</b>
/// "50,000" means nothing on its own, and a platform whose users are comparing offers across
/// countries and currencies cannot afford the ambiguity. The period plus the currency is what
/// lets a candidate in Manila and a candidate in Warsaw read the same posting and both
/// understand it.
/// </para>
/// </summary>
public enum SalaryPeriod
{
    Hourly = 1,

    Daily = 2,

    Weekly = 3,

    Monthly = 4,

    /// <summary>Per annum. The default most professional postings quote in.</summary>
    Yearly = 5,

    /// <summary>A fixed sum for the whole engagement. Contract and freelance work.</summary>
    Project = 6
}
