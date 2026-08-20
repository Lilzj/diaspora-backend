using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Providers.Persistence.Configurations;

/// <summary>
/// The converters every Providers configuration shares.
/// <para>
/// Declared once rather than inline, for the reason B5 records and B6 repeats: the storage form
/// of a value object is a property of the value object, and two inline converters that differ in
/// one detail produce rows that no longer join.
/// </para>
/// </summary>
internal static class ProviderValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();

    /// <summary>
    /// <see cref="TimeOnly"/> as minutes past midnight.
    /// <para>
    /// A short integer rather than a provider-native time type, because SQLite has none and
    /// PostgreSQL's, SQL Server's and MySQL's all differ in precision and in what they do with a
    /// value of 24:00. Minutes past midnight is the same number everywhere, sorts and compares
    /// natively, and is exactly the granularity opening hours are ever expressed in — nobody
    /// opens at 09:00:30.
    /// </para>
    /// </summary>
    public static ValueConverter<TimeOnly, int> TimeOfDay { get; } =
        new(
            time => (time.Hour * 60) + time.Minute,
            minutes => new TimeOnly(minutes / 60, minutes % 60));

    /// <summary>
    /// <see cref="DateOnly"/> as a day number.
    /// <para>
    /// Same reasoning as <see cref="TimeOfDay"/>: SQLite stores it as text and orders it
    /// lexically, which happens to work for ISO dates and stops working the moment a provider
    /// runs on a database whose text collation is not ordinal. An integer day number sorts
    /// correctly under every collation there is.
    /// </para>
    /// </summary>
    public static ValueConverter<DateOnly, int> Date { get; } =
        new(
            date => date.DayNumber,
            days => DateOnly.FromDayNumber(days));

    /// <summary>The nullable form. EF will not reuse a non-nullable converter for a nullable column.</summary>
    public static ValueConverter<DateOnly?, int?> NullableDate { get; } =
        new(
            date => date == null ? null : date.Value.DayNumber,
            days => days == null ? null : DateOnly.FromDayNumber(days.Value));
}
