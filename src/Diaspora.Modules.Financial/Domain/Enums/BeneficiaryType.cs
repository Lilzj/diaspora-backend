namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Who receives a donation or a contribution.</summary>
public enum BeneficiaryType
{
    Member = 1,
    Organization = 2,
    Community = 3,

    /// <summary>A named fund this module owns.</summary>
    Fund = 4,

    /// <summary>The platform itself.</summary>
    Platform = 5
}
