namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What sort of party is letting the accommodation.
/// <para>
/// <b>The kind drives which Trust claim is submitted and what a badge means.</b> An individual is
/// verified as a person; an agency, a hostel or an employer is verified as a business against the
/// landlord profile rather than the member behind it - the distinction B3.0.2's subject model
/// exists to make, because one member may run two agencies.
/// </para>
/// <para>
/// The commercial kinds are also the ones that may be backed by a B7 provider. Ownership of the
/// business itself stays in Providers; this records only which provider a landlord belongs to.
/// </para>
/// </summary>
public enum LandlordKind
{
    /// <summary>A person letting their own property or a room in it.</summary>
    Individual = 0,

    EstateAgent = 1,
    PropertyManager = 2,
    Hostel = 3,
    Hotel = 4,
    RelocationCompany = 5,
    StudentHousing = 6,

    /// <summary>An employer housing its own staff.</summary>
    CorporateHousing = 7
}
