namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>Whether a landlord may still let property on the platform.</summary>
public enum LandlordStatus
{
    Active = 0,

    /// <summary>Barred from listing by moderation. Existing tenants' records survive.</summary>
    Suspended = 1,

    /// <summary>Closed by the landlord. Reopenable.</summary>
    Closed = 2
}
