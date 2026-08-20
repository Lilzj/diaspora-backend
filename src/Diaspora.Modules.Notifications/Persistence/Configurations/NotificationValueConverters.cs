using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// The converters every Notifications configuration shares.
/// <para>
/// Declared once rather than inline: the storage form of an identifier is a property of the
/// identifier, and two inline converters that differ in one detail produce rows that no longer join.
/// </para>
/// </summary>
internal static class NotificationValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
