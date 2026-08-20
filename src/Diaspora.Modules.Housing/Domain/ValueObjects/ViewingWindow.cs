using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// When a viewing is meant to happen, and how.
/// <para>
/// <b>The preferred time is the member's ask and the confirmed time is the landlord's answer, and
/// they are two fields rather than one.</b> Overwriting the request with the confirmation loses
/// the fact that a landlord offered Tuesday to somebody who asked for Saturday, which is exactly
/// what a member disputing a no-show needs to point at.
/// </para>
/// <para>
/// <b><see cref="IsVirtual"/> is not a nicety.</b> Most of the people this module serves are
/// choosing a home from another country, and a video walk-through is the only viewing they can
/// have before they fly. A workflow that assumed physical attendance would exclude the whole
/// primary use case.
/// </para>
/// </summary>
public sealed class ViewingWindow : ValueObject
{
    private ViewingWindow(
        DateTimeOffset? preferredAt,
        DateTimeOffset? confirmedAt,
        int durationMinutes,
        bool isVirtual,
        string? place)
    {
        PreferredAt = preferredAt;
        ConfirmedAt = confirmedAt;
        DurationMinutes = durationMinutes;
        IsVirtual = isVirtual;
        Place = place;
    }

    private ViewingWindow() { }

    /// <summary>When the member asked for. Null when they left it to the landlord.</summary>
    public DateTimeOffset? PreferredAt { get; private set; }

    /// <summary>When the landlord agreed to. Null until they answer.</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    public int DurationMinutes { get; private set; }

    /// <summary>A video walk-through rather than a visit. See the class summary.</summary>
    public bool IsVirtual { get; private set; }

    /// <summary>
    /// Where to meet, or how to join. Free text and deliberately not the property address: the
    /// address is disclosed by <c>PropertyAddress.Disclose</c> once this viewing is confirmed, and
    /// duplicating it here would be a second copy with its own privacy rules to get wrong.
    /// </summary>
    public string? Place { get; private set; }

    /// <summary>The time the parties are actually working to.</summary>
    public DateTimeOffset? EffectiveAt => ConfirmedAt ?? PreferredAt;

    /// <summary>When the slot ends. Null when no time is set at all.</summary>
    public DateTimeOffset? EndsAt =>
        EffectiveAt is { } start ? start.AddMinutes(DurationMinutes) : null;

    /// <summary>Whether the slot has passed. What the expiry sweep asks.</summary>
    public bool HasLapsed(DateTimeOffset now) => EndsAt is { } ends && ends < now;

    public static Result<ViewingWindow> Create(
        DateTimeOffset? preferredAt,
        DateTimeOffset? confirmedAt = null,
        int durationMinutes = 30,
        bool isVirtual = false,
        string? place = null)
    {
        if (durationMinutes is < 5 or > 480)
        {
            return Result.Failure<ViewingWindow>(HousingErrors.ViewingWindowInvalid);
        }

        var trimmed = Clamp(place, HousingLimits.MaxViewingNote);

        if (trimmed is null && !string.IsNullOrWhiteSpace(place))
        {
            return Result.Failure<ViewingWindow>(HousingErrors.ViewingWindowInvalid);
        }

        return Result.Success(
            new ViewingWindow(preferredAt, confirmedAt, durationMinutes, isVirtual, trimmed));
    }

    /// <summary>The landlord agreeing a time. Keeps the original ask - see the class summary.</summary>
    public ViewingWindow Confirm(DateTimeOffset confirmedAt, string? place = null) =>
        new(PreferredAt,
            confirmedAt,
            DurationMinutes,
            IsVirtual,
            Clamp(place, HousingLimits.MaxViewingNote) ?? Place);

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PreferredAt;
        yield return ConfirmedAt;
        yield return DurationMinutes;
        yield return IsVirtual;
        yield return Place;
    }
}
