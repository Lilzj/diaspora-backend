using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Diaspora.Persistence.Atomic;

/// <summary>
/// A mapped column, reached from an aggregate through a property path.
/// </summary>
/// <param name="Property">
/// EF's own metadata for the column — the source of both its value converter and its store type,
/// so a parameter built from it is correct on every provider without this layer knowing which one
/// is running.
/// </param>
/// <param name="ColumnName">The undelimited column name.</param>
/// <param name="Navigations">
/// The owned reference navigations crossed on the way, in order. Empty for a property on the root.
/// Kept because reconciling the change tracker afterwards has to walk the same route.
/// </param>
/// <param name="PropertyName">The final segment.</param>
internal sealed record AtomicColumn(
    IProperty Property,
    string ColumnName,
    IReadOnlyList<string> Navigations,
    string PropertyName);

/// <summary>
/// The table one aggregate's guarded updates address, and the single-column key that names a row
/// in it.
/// </summary>
internal sealed record AtomicTarget(
    IEntityType EntityType,
    string TableName,
    string? Schema,
    StoreObjectIdentifier StoreObject,
    IProperty KeyProperty,
    string KeyColumnName);

/// <summary>
/// Turns property paths into columns, using EF's model and nothing else.
/// <para>
/// <b>Metadata rather than convention is what makes the primitive provider-neutral.</b> The column
/// behind <c>Availability.ReservedUnits</c> is whatever the module's configuration said it was —
/// a different name per module, a different delimiter per provider — and asking the model removes
/// every opportunity for this layer to guess. It is the same technique the transaction receipt's
/// insert already uses, and for the same reason.
/// </para>
/// <para>
/// Owned value objects are crossed transparently: they are separate entity types in the model and
/// the same row in the database, which is precisely why a counter living inside one can be updated
/// by a statement addressing the aggregate's table.
/// </para>
/// </summary>
internal static class AtomicColumnResolver
{
    private static readonly ConditionalWeakTable<IModel, ModelCache> Caches = [];

    /// <summary>The table and key an aggregate's guarded updates run against.</summary>
    public static AtomicTarget Target(IModel model, Type entityClrType)
    {
        var cache = Caches.GetValue(model, static _ => new ModelCache());

        return cache.Targets.GetOrAdd(entityClrType, type => BuildTarget(model, type));
    }

    /// <summary>The column one property path names on that table.</summary>
    public static AtomicColumn Column(IModel model, Type entityClrType, string path)
    {
        var cache = Caches.GetValue(model, static _ => new ModelCache());

        return cache.Columns.GetOrAdd(
            (entityClrType, path),
            key => BuildColumn(Target(model, key.EntityType), key.Path));
    }

    private static AtomicTarget BuildTarget(IModel model, Type entityClrType)
    {
        var entityType = model.FindEntityType(entityClrType)
            ?? throw new InvalidOperationException(
                $"'{entityClrType.Name}' is not mapped. A guarded update addresses a table, so the " +
                "entity must be part of the model.");

        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException(
                $"'{entityClrType.Name}' is not mapped to a table and cannot be updated by " +
                "statement.");

        var schema = entityType.GetSchema();
        var storeObject = StoreObjectIdentifier.Table(tableName, schema);

        var key = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException(
                $"'{entityClrType.Name}' has no primary key, so no statement can name one of its " +
                "rows.");

        if (key.Properties.Count != 1)
        {
            throw new InvalidOperationException(
                $"'{entityClrType.Name}' has a composite primary key. Guarded updates address a " +
                "single-column key, which every aggregate on this platform has.");
        }

        var keyProperty = key.Properties[0];

        return new AtomicTarget(
            entityType,
            tableName,
            schema,
            storeObject,
            keyProperty,
            keyProperty.GetColumnName(storeObject)
                ?? throw new InvalidOperationException(
                    $"The key of '{entityClrType.Name}' has no column on {tableName}."));
    }

    private static AtomicColumn BuildColumn(AtomicTarget target, string path)
    {
        var segments = path.Split('.');
        var entityType = target.EntityType;
        var navigations = new List<string>(segments.Length - 1);

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var navigation = entityType.FindNavigation(segments[index])
                ?? throw new InvalidOperationException(
                    $"'{segments[index]}' is not a navigation on '{entityType.ShortName()}', so " +
                    $"'{path}' names no column.");

            if (!navigation.TargetEntityType.IsOwned() || navigation.IsCollection)
            {
                throw new InvalidOperationException(
                    $"'{path}' crosses '{segments[index]}', which is not an owned reference. A " +
                    "guarded update changes one row, and only an owned reference shares the " +
                    "aggregate's row.");
            }

            navigations.Add(navigation.Name);
            entityType = navigation.TargetEntityType;
        }

        var propertyName = segments[^1];

        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"'{propertyName}' is not a mapped property of '{entityType.ShortName()}'.");

        var column = property.GetColumnName(target.StoreObject)
            ?? throw new InvalidOperationException(
                $"'{path}' has no column on '{target.TableName}'. An owned type mapped to its own " +
                "table cannot take part in a guarded update of the aggregate's row.");

        return new AtomicColumn(property, column, navigations, propertyName);
    }

    private sealed class ModelCache
    {
        public ConcurrentDictionary<Type, AtomicTarget> Targets { get; } = new();

        public ConcurrentDictionary<(Type EntityType, string Path), AtomicColumn> Columns { get; } =
            new();
    }
}
