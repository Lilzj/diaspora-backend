using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// The converters every Marketplace configuration shares.
/// <para>
/// Declared once rather than inline, for the reason B5 records and B6, B7, B8 and B9 each repeat:
/// the storage form of a value object is a property of the value object, and two inline converters
/// that differ in one detail produce rows that no longer join.
/// </para>
/// </summary>
internal static class MarketplaceValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
