namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// What kind of official body issued something.
/// <para>
/// <b>An enum where notice categories are a seeded catalogue, and the asymmetry is deliberate.</b>
/// A category decides how a notice is presented and which of them a reader has asked to hear about,
/// so a deployment must be able to add one. A type decides how much weight the platform gives the
/// claim, and letting a deployment add "Community Elder" beside "Immigration Department" would make
/// the badge on an official notice mean whatever the last administrator decided it meant.
/// </para>
/// <para>
/// <see cref="Other"/> is the escape hatch, and it is the honest one: the body is named on the
/// record either way, and a type nobody can express is a record somebody enters under the closest
/// wrong option.
/// </para>
/// </summary>
public enum AuthorityType
{
    /// <summary>A resident diplomatic mission.</summary>
    Embassy = 0,

    /// <summary>A Commonwealth mission. The same function, a different name, and staff use it.</summary>
    HighCommission = 1,

    /// <summary>A consular post, usually subordinate to an embassy.</summary>
    Consulate = 2,

    /// <summary>A department of national government.</summary>
    GovernmentMinistry = 3,

    /// <summary>The body that decides entry, residence and status.</summary>
    ImmigrationDepartment = 4,

    /// <summary>The national health authority.</summary>
    HealthMinistry = 5,

    /// <summary>A police service.</summary>
    Police = 6,

    /// <summary>Fire, ambulance, civil protection and disaster management.</summary>
    EmergencyServices = 7,

    /// <summary>The revenue authority.</summary>
    TaxAuthority = 8,

    /// <summary>A ministry, board or council governing education.</summary>
    EducationAuthority = 9,

    /// <summary>The department responsible for employment and work permits.</summary>
    LaborDepartment = 10,

    /// <summary>A city, council or other local government.</summary>
    Municipality = 11,

    /// <summary>An official body none of the above describes. Named on the record.</summary>
    Other = 12
}
