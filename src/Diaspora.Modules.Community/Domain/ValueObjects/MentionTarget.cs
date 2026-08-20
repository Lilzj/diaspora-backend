using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// What an <c>@handle</c> in a body turned out to point at.
/// <para>
/// Stores the handle as typed alongside the resolution, and both matter. The handle is what the
/// body says and what must be re-rendered; the resolution is what the notification goes to. A
/// mention that resolved yesterday and whose target has since been deleted still has to render,
/// and it renders as the text somebody actually wrote.
/// </para>
/// <para>
/// An unresolved mention is stored, not discarded. Discarding it would mean a member who typed
/// a name nobody claims sees their text silently altered — and it would mean the same body
/// re-resolves differently the day a module registers a resolver for that handle, with no
/// record that it ever did not.
/// </para>
/// </summary>
public sealed class MentionTarget : ValueObject
{
    public const int MaxHandleLength = 128;

    private MentionTarget(
        MentionTargetType type, string handle, Guid? targetId, string? displayName)
    {
        Type = type;
        Handle = handle;
        TargetId = targetId;
        DisplayName = displayName;
    }

    private MentionTarget() { }

    public MentionTargetType Type { get; private set; }

    /// <summary>The handle as typed, without the leading <c>@</c>.</summary>
    public string Handle { get; private set; } = string.Empty;

    /// <summary>Null when nothing claimed the handle.</summary>
    public Guid? TargetId { get; private set; }

    /// <summary>Captured at resolution time so a render needs no cross-module lookup.</summary>
    public string? DisplayName { get; private set; }

    public bool IsResolved => Type != MentionTargetType.Unresolved && TargetId is not null;

    public static MentionTarget Unresolved(string handle) =>
        new(MentionTargetType.Unresolved, Normalise(handle), null, null);

    public static MentionTarget Resolved(
        MentionTargetType type, string handle, Guid targetId, string? displayName) =>
        new(type, Normalise(handle), targetId, displayName);

    private static string Normalise(string? handle)
    {
        var trimmed = (handle ?? string.Empty).Trim().TrimStart('@');

        return trimmed.Length > MaxHandleLength
            ? trimmed[..MaxHandleLength]
            : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Handle;
        yield return TargetId;
    }
}
