using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// One live home's asking rent, as the rent statistics read it: the amount, its currency and
/// period, and where the home is filed. No identity and nothing a member wrote.
/// </summary>
public sealed record HousingRentPrice(
    string? AreaPath,
    decimal Amount,
    string CurrencyCode,
    RentPeriod Period,
    DateTimeOffset? PublishedAt);
