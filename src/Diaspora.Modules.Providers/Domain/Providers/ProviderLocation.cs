using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One physical place a provider operates from, beyond its principal one.
/// <para>
/// The aggregate carries a single <see cref="ProviderLocationContext"/> as its principal
/// address — which is what the client renders and what discovery sorts by — and this collection
/// carries the rest. A church with two halls, a takeaway with three branches and a solicitor
/// with a second office in another city are all one provider with one reputation, one owner and
/// several addresses; modelling them as several providers would fragment the reviews and make
/// the same business compete with itself in search.
/// </para>
/// <para>
/// Each branch may keep its own hours and its own contact number, which is why
/// <see cref="OperatingHoursNote"/> and <see cref="PhoneNumber"/> are here rather than assumed
/// shared. Full per-branch schedules are a deliberate non-goal for B7 — the weekly schedule
/// lives on the provider, and a future scheduling module can move it down without the contract
/// changing, because a branch already has somewhere to put one.
/// </para>
/// </summary>
public sealed class ProviderLocation : Entity<ProviderLocationId>
{
    public const int MaxNameLength = 200;

    public const int MaxNoteLength = 500;

    public const int MaxPhoneLength = 32;

    private ProviderLocation(ProviderLocationId id, string name, ProviderLocationContext place)
        : base(id)
    {
        Name = name;
        Place = place;
    }

    private ProviderLocation() { }

    /// <summary>"Peckham branch", "Main hall", "Ebène office".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Where it is, denormalised from Location's catalogue.</summary>
    public ProviderLocationContext Place { get; private set; } = ProviderLocationContext.None;

    /// <summary>Whether this is the address discovery treats as the provider's own.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Whether members may turn up here. False for a registered office.</summary>
    public bool IsPubliclyVisible { get; private set; } = true;

    /// <summary>Hours in prose, for a branch whose schedule differs from the provider's.</summary>
    public string? OperatingHoursNote { get; private set; }

    /// <summary>A direct line for this branch.</summary>
    public string? PhoneNumber { get; private set; }

    public int Position { get; private set; }

    public static ProviderLocation Create(
        ProviderLocationId id,
        string? name,
        ProviderLocationContext place,
        bool isPrimary = false,
        bool isPubliclyVisible = true,
        int position = 0)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(place);

        return new ProviderLocation(id, Trim(name, MaxNameLength) ?? "Main", place)
        {
            IsPrimary = isPrimary,
            IsPubliclyVisible = isPubliclyVisible,
            Position = position < 0 ? 0 : position
        };
    }

    public void Update(
        string? name,
        ProviderLocationContext place,
        bool isPubliclyVisible,
        string? operatingHoursNote,
        string? phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(place);

        Name = Trim(name, MaxNameLength) ?? Name;
        Place = place;
        IsPubliclyVisible = isPubliclyVisible;
        OperatingHoursNote = Trim(operatingHoursNote, MaxNoteLength);
        PhoneNumber = Trim(phoneNumber, MaxPhoneLength);
    }

    /// <summary>Internal: only the aggregate may decide which branch is principal.</summary>
    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    internal void SetPosition(int position) => Position = position < 0 ? 0 : position;

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
