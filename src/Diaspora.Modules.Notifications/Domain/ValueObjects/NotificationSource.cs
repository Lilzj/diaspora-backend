using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// What this message is about, expressed so that Communication never learns what any of it means.
/// <para>
/// <b>Three strings and no types, deliberately.</b> The moment this held a <c>JobId</c> the module
/// would need a reference to Jobs, and the phase after that it would need one to Marketplace,
/// Housing and Guides — and the acyclic module graph B2.6 exists to protect would be gone for the
/// sake of rendering a link. A notification about a job knows the word "job", the identifier as a
/// string, and nothing else.
/// </para>
/// <para>
/// The pair is also the de-duplication key: two published events about the same interview must not
/// produce two identical inbox rows, and "same source, same template, same recipient, within the
/// window" is what makes that decidable without any module-specific knowledge.
/// </para>
/// </summary>
public sealed class NotificationSource : ValueObject
{
    private NotificationSource(
        string module, string? eventName, string? subjectType, string? subjectId)
    {
        Module = module;
        EventName = eventName;
        SubjectType = subjectType;
        SubjectId = subjectId;
    }

    private NotificationSource() { }

    /// <summary>The module that published the fact — <c>Jobs</c>, <c>Trust</c>, <c>Notifications</c>.</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>The published contract's type name. Null for a direct dispatch.</summary>
    public string? EventName { get; private set; }

    /// <summary>What kind of record it concerns — <c>interview</c>, <c>listing</c>, <c>verification</c>.</summary>
    public string? SubjectType { get; private set; }

    /// <summary>Its identifier as a string. Never parsed here.</summary>
    public string? SubjectId { get; private set; }

    public static NotificationSource Create(
        string module, string? eventName = null, string? subjectType = null, string? subjectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        return new NotificationSource(
            Clip(module)!,
            Clip(eventName),
            Clip(subjectType),
            Clip(subjectId));
    }

    /// <summary>The stable string two notifications are compared on for de-duplication.</summary>
    public string DeduplicationKey =>
        string.Join('|', Module, SubjectType ?? "-", SubjectId ?? "-");

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= NotificationLimits.MaxSubjectIdLength
            ? trimmed
            : trimmed[..NotificationLimits.MaxSubjectIdLength];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Module;
        yield return EventName;
        yield return SubjectType;
        yield return SubjectId;
    }
}
