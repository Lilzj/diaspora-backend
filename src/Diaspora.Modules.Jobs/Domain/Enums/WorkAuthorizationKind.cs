namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// A candidate's right to work in a particular country.
/// <para>
/// <b>Per country, never global.</b> A candidate holds a set of these, one per jurisdiction,
/// because the whole point of a diaspora employment platform is that the answer differs by
/// destination. Modelling it as a single field on the profile — as most job products do — is
/// what forces members to explain their situation in a cover letter every single time.
/// </para>
/// <para>
/// This is the candidate's own statement about their status. It is never a verified claim:
/// Trust owns verification, and nothing here is evidence of anything. It exists so a posting
/// that sponsors and a candidate who needs sponsorship can find each other.
/// </para>
/// </summary>
public enum WorkAuthorizationKind
{
    /// <summary>A citizen of the country.</summary>
    Citizen = 1,

    /// <summary>Settled or permanent-resident status.</summary>
    PermanentResident = 2,

    /// <summary>Holds a work permit or visa that allows this work.</summary>
    WorkPermit = 3,

    /// <summary>A student visa with limited working rights.</summary>
    StudentVisa = 4,

    /// <summary>A dependant or spouse visa carrying working rights.</summary>
    DependantVisa = 5,

    /// <summary>Free movement or a treaty right.</summary>
    TreatyRight = 6,

    /// <summary>Eligible to work only if the employer sponsors.</summary>
    RequiresSponsorship = 7,

    /// <summary>An arrangement the platform has not catalogued.</summary>
    Other = 99
}
