namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>How often a standing alert is delivered.</summary>
public enum JobAlertFrequency
{
    /// <summary>As soon as a matching posting is published.</summary>
    Immediate = 1,

    Daily = 2,

    Weekly = 3
}
