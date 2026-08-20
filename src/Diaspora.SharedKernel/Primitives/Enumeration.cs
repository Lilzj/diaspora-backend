using System.Reflection;

namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// Smart enumeration pattern. Provides type-safe enums with behavior,
/// replacing primitive enum types.
/// </summary>
public abstract class Enumeration : IComparable<Enumeration>, IEquatable<Enumeration>
{
    public int Id { get; }
    public string Name { get; }

    protected Enumeration(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<T>();

    public static T FromId<T>(int id) where T : Enumeration
    {
        var matchingItem = GetAll<T>().FirstOrDefault(item => item.Id == id);
        return matchingItem ?? throw new InvalidOperationException(
            $"'{id}' is not a valid ID for {typeof(T).Name}.");
    }

    public static T FromName<T>(string name) where T : Enumeration
    {
        var matchingItem = GetAll<T>().FirstOrDefault(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return matchingItem ?? throw new InvalidOperationException(
            $"'{name}' is not a valid name for {typeof(T).Name}.");
    }

    public static bool TryFromId<T>(int id, out T? result) where T : Enumeration
    {
        result = GetAll<T>().FirstOrDefault(item => item.Id == id);
        return result is not null;
    }

    public static bool TryFromName<T>(string name, out T? result) where T : Enumeration
    {
        result = GetAll<T>().FirstOrDefault(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return result is not null;
    }

    public int CompareTo(Enumeration? other) => other is null ? 1 : Id.CompareTo(other.Id);

    public bool Equals(Enumeration? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return GetType() == other.GetType() && Id == other.Id;
    }

    public override bool Equals(object? obj) =>
        obj is Enumeration enumeration && Equals(enumeration);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Enumeration? left, Enumeration? right) =>
        Equals(left, right);

    public static bool operator !=(Enumeration? left, Enumeration? right) =>
        !Equals(left, right);

    // Ordering operators to match IComparable. Note that ordering compares Id, which is
    // only meaningful within one enumeration type — AccessState.Verified > UserRole.Admin
    // is a nonsense question that this will nonetheless answer. Callers that care about
    // rank should use the domain's own predicates (IsOnboarded, IsStaff) rather than these.
    public static bool operator <(Enumeration? left, Enumeration? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(Enumeration? left, Enumeration? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(Enumeration? left, Enumeration? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(Enumeration? left, Enumeration? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;
}
