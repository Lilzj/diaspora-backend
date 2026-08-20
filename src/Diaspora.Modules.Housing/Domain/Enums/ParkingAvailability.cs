namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>What parking the property has, if any.</summary>
public enum ParkingAvailability
{
    Unknown = 0,
    None = 1,
    StreetParking = 2,
    Driveway = 3,
    AllocatedSpace = 4,
    Garage = 5,
    PaidNearby = 6
}
