using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// The converters every Housing configuration shares.
/// <para>
/// Declared once rather than inline, for the reason B5 records and every module since repeats: the
/// storage form of a value object is a property of the value object, and two inline converters
/// that differ in one detail produce rows that no longer join.
/// </para>
/// </summary>
internal static class HousingValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
