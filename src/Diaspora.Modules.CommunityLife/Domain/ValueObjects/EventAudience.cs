using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// Who an event is for and what to expect when you get there.
/// <para>
/// <b>These are filters, not decoration.</b> "Family friendly" and "student" are two of the search
/// facets the brief names by name, and a member deciding whether to bring a four-year-old to a
/// naming ceremony is asking a question the platform should be able to answer without them
/// reading four paragraphs of description.
/// </para>
/// <para>
/// <b>The accessibility note is free text and is required to stay so.</b> An enum of accessibility
/// features would be wrong everywhere — step-free access means something different in a converted
/// church hall than in a conference centre — and an organizer writing "ramp at the side entrance,
/// ask for Grace" is telling somebody exactly what they need to know.
/// </para>
/// </summary>
public sealed class EventAudience : ValueObject
{
    private EventAudience(
        bool isFamilyFriendly,
        bool isChildFriendly,
        bool isStudentFocused,
        bool isFree,
        string? dressCode,
        string? accessibilityNotes)
    {
        IsFamilyFriendly = isFamilyFriendly;
        IsChildFriendly = isChildFriendly;
        IsStudentFocused = isStudentFocused;
        IsFree = isFree;
        DressCode = dressCode;
        AccessibilityNotes = accessibilityNotes;
    }

    private EventAudience() { }

    /// <summary>The default: free, open to all, nothing said about dress or access.</summary>
    public static EventAudience Default => new(false, false, false, true, null, null);

    public bool IsFamilyFriendly { get; private set; }

    /// <summary>
    /// Narrower than family friendly, and different: a wedding reception welcomes families and is
    /// not somewhere a toddler is catered for.
    /// </summary>
    public bool IsChildFriendly { get; private set; }

    public bool IsStudentFocused { get; private set; }

    /// <summary>
    /// Whether there is anything to pay at the door.
    /// <para>
    /// <b>A flag, not a price.</b> The brief puts ticket payments and paid events explicitly out of
    /// scope, and a price field would be the first half of a payment system nobody asked for. What
    /// members need to filter on is "free events", which this answers, and what an organizer needs
    /// to say about a contribution belongs in the description until a commerce module exists.
    /// </para>
    /// </summary>
    public bool IsFree { get; private set; }

    public string? DressCode { get; private set; }

    public string? AccessibilityNotes { get; private set; }

    public static Result<EventAudience> Create(
        bool isFamilyFriendly = false,
        bool isChildFriendly = false,
        bool isStudentFocused = false,
        bool isFree = true,
        string? dressCode = null,
        string? accessibilityNotes = null) =>
        Result.Success(new EventAudience(
            isFamilyFriendly || isChildFriendly,
            isChildFriendly,
            isStudentFocused,
            isFree,
            Clip(dressCode, CommunityLifeLimits.MaxDressCode),
            Clip(accessibilityNotes, CommunityLifeLimits.MaxAccessibilityNotes)));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsFamilyFriendly;
        yield return IsChildFriendly;
        yield return IsStudentFocused;
        yield return IsFree;
        yield return DressCode;
        yield return AccessibilityNotes;
    }
}
