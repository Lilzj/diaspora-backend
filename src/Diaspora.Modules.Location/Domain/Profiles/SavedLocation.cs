using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>
/// A place the member has named and kept — "Home", "Work", "School", or anything else.
/// <para>
/// The label is a free string with three conventional values rather than an enum, and that
/// is the frontend's own shape: <c>savedLocations</c> is a
/// <c>Record&lt;string, PlatformLocation | null&gt;</c> seeded with three keys. An enum here
/// would mean a member who wants "Mum's" has to be told no by the type system, which is a
/// poor reason to say no.
/// </para>
/// </summary>
public sealed class SavedLocation : Entity<SavedLocationId>
{
    public const int MaxLabelLength = 40;

    /// <summary>The three the frontend seeds. Not exhaustive — a member may add their own.</summary>
    public static readonly IReadOnlyList<string> ConventionalLabels = ["Home", "Work", "School"];

    public LocationProfileId ProfileId { get; private set; } = default!;

    /// <summary>Unique within the profile, case-insensitively.</summary>
    public string Label { get; private set; } = default!;

    public LocationSnapshot Location { get; private set; } = default!;

    public DateTimeOffset SavedAt { get; private set; }

    private SavedLocation() { }

    private SavedLocation(
        SavedLocationId id,
        LocationProfileId profileId,
        string label,
        LocationSnapshot location,
        DateTimeOffset savedAt) : base(id)
    {
        ProfileId = profileId;
        Label = label;
        Location = location;
        SavedAt = savedAt;
    }

    internal static SavedLocation Create(
        LocationProfileId profileId,
        string label,
        LocationSnapshot location,
        DateTimeOffset savedAt)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentNullException.ThrowIfNull(location);

        return new SavedLocation(
            SavedLocationId.New(), profileId, Normalize(label), location, savedAt);
    }

    internal void MoveTo(LocationSnapshot location, DateTimeOffset savedAt)
    {
        ArgumentNullException.ThrowIfNull(location);

        Location = location;
        SavedAt = savedAt;
    }

    /// <summary>
    /// Trims and caps the label, preserving the member's own capitalisation.
    /// <para>
    /// Comparison is case-insensitive but storage is not: a member who typed "home" should
    /// see "home", and should still be prevented from also creating "Home".
    /// </para>
    /// </summary>
    private static string Normalize(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var trimmed = label.Trim();
        return trimmed.Length <= MaxLabelLength ? trimmed : trimmed[..MaxLabelLength];
    }
}
