using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;

namespace Diaspora.Modules.Guides.Domain.Holidays;

/// <summary>
/// How the application layer reaches the public holiday calendar.
/// <para>
/// <b>No cursor paging, and that is a considered omission rather than a gap.</b> A country has
/// somewhere between ten and thirty holidays in a year. The two real queries are "this country's
/// calendar for a year" and "what is coming up in the next ninety days", and both return a set small
/// enough to hand over whole — a cursor over twenty rows is machinery nobody needs and a second
/// ordering nobody can see is wrong.
/// </para>
/// </summary>
public interface IPublicHolidayRepository
{
    Task<PublicHoliday?> GetAsync(
        PublicHolidayId id, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.GetIncludingArchivedAsync" />
    Task<PublicHoliday?> GetIncludingArchivedAsync(
        PublicHolidayId id, CancellationToken cancellationToken = default);

    /// <summary>One country's calendar for one year, in date order.</summary>
    Task<IReadOnlyList<PublicHoliday>> ListForYearAsync(
        string countryCode, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// What falls between two instants, in date order.
    /// <para>
    /// A range rather than a horizon in days, because the dashboard's ninety-day window and an
    /// editor's "show me next quarter" are the same query with different arguments, and expressing
    /// one of them as a day count would make the other awkward.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PublicHoliday>> ListBetweenAsync(
        string countryCode,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);

    Task AddAsync(PublicHoliday holiday, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.Remove" />
    void Remove(PublicHoliday holiday);
}
