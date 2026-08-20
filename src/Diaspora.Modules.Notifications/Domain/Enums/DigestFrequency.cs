namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>How often a member wants their low-urgency messages collected.</summary>
public enum DigestFrequency
{
    /// <summary>No digest. Everything is delivered on its own terms.</summary>
    Off = 0,

    Daily = 1,

    Weekly = 2,

    /// <summary>Generated on request rather than on a schedule. Also what an operator triggers.</summary>
    Manual = 3
}
