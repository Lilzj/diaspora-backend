namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Why somebody reported a listing.
/// <para>
/// <b><see cref="Scam"/> is first and is treated differently everywhere it appears.</b> A rental
/// advance-fee fraud takes a newcomer's deposit before they have a bank account, a job or anyone
/// to ask, and it is the single most damaging piece of content this platform can carry. One
/// report of it flags; every other reason waits for the configured threshold.
/// </para>
/// </summary>
public enum HousingReportReason
{
    Scam = 0,
    Duplicate = 1,
    Misleading = 2,
    AlreadyTaken = 3,
    WrongLocation = 4,
    Discriminatory = 5,
    UnsafeProperty = 6,
    Offensive = 7,
    NotTheOwner = 8,
    Other = 99
}
