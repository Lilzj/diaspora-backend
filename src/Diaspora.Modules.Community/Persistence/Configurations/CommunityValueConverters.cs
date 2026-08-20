using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// The converters every Community configuration shares.
/// <para>
/// Declared once rather than inline, for the reason B5 records: the storage form of a value
/// object is a property of the value object, and two inline converters that differ in one detail
/// produce rows that no longer join.
/// </para>
/// </summary>
internal static class CommunityValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
