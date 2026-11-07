using System.Collections.Concurrent;
using System.Reflection;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Reads what an audited request acted on, and the reason it gave, from the request itself.
/// <para>
/// <b>The entity id was always empty</b> (until 7 Nov 2026): the pipeline knew the command's name
/// and nothing about its target, so a trail of "SuspendAccount by an admin" could not say whose
/// account. Commands name their target in a property ending in <c>Id</c> — <c>UserId</c>,
/// <c>PostingId</c>, <c>ItemId</c> — as a <see cref="Guid"/>, a string, or a strongly typed id
/// wrapping one. The first such property, in declaration order, is the subject; a property the
/// request carries for bookkeeping (a correlation or idempotency key) is not.
/// </para>
/// <para>
/// The reason is the first non-empty string property named like one: <c>Reason</c>, <c>Note</c>,
/// <c>Notes</c>, <c>Explanation</c>, <c>DecisionNote</c>. Both are looked for on the command and,
/// failing that, on the HTTP body it carries as <c>Request</c>.
/// </para>
/// </summary>
public static class AuditSubject
{
    private static readonly string[] ReasonNames =
        ["Reason", "Note", "Notes", "Explanation", "DecisionNote", "ModerationNote"];

    private static readonly HashSet<string> NotSubjects = new(StringComparer.Ordinal)
    {
        "CorrelationId", "IdempotencyKey", "RequestId", "ExpectedRevision"
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> SubjectCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReasonCache = new();

    public static string? EntityIdOf(object request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var property = SubjectCache.GetOrAdd(request.GetType(), FindSubject);

        if (property is not null && Describe(property.GetValue(request)) is { } found)
        {
            return found;
        }

        // Most moderation commands wrap the HTTP body: { ListingId, Request { Action, Reason } }.
        return BodyOf(request) is { } body ? EntityIdOf(body) : null;
    }

    /// <summary>The subject named by a successful result's value, for a request that named none.</summary>
    public static string? EntityIdOfResult(object? response)
    {
        if (response is null)
        {
            return null;
        }

        var value = response.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(response);

        return value is null or string ? null : EntityIdOf(value);
    }

    public static string? ReasonOf(object request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var property in ReasonCache.GetOrAdd(request.GetType(), FindReasons))
        {
            if (property.GetValue(request) is string { Length: > 0 } reason
                && !string.IsNullOrWhiteSpace(reason))
            {
                return reason.Trim();
            }
        }

        return BodyOf(request) is { } body ? ReasonOf(body) : null;
    }

    private static object? BodyOf(object request) =>
        request.GetType().GetProperty("Request", BindingFlags.Public | BindingFlags.Instance)
            is { } property
        && property.PropertyType.IsClass
        && property.PropertyType != typeof(string)
            ? property.GetValue(request)
            : null;

    private static PropertyInfo? FindSubject(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property => property.Name.EndsWith("Id", StringComparison.Ordinal)
                && !NotSubjects.Contains(property.Name))
            .OrderBy(property => property.MetadataToken)
            .FirstOrDefault(property => IsIdentifier(property.PropertyType));

    private static PropertyInfo[] FindReasons(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string)
                && property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        return [.. ReasonNames.Where(properties.ContainsKey).Select(name => properties[name])];
    }

    private static bool IsIdentifier(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying == typeof(Guid)
            || underlying == typeof(string)
            || WrappedValue(underlying) is not null;
    }

    private static PropertyInfo? WrappedValue(Type type) =>
        type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance) is { } value
        && (value.PropertyType == typeof(Guid) || value.PropertyType == typeof(string))
            ? value
            : null;

    private static string? Describe(object? value) => value switch
    {
        null => null,
        Guid guid => guid == Guid.Empty ? null : guid.ToString(),
        string text => string.IsNullOrWhiteSpace(text) ? null : text,
        _ when WrappedValue(value.GetType()) is { } inner => Describe(inner.GetValue(value)),
        _ => value.ToString()
    };
}
