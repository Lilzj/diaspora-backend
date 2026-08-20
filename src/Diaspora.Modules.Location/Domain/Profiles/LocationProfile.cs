using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Events;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>
/// A member's geographic context: where they are, where they are from, where they have
/// saved, and how they want proximity to behave.
/// <para>
/// All of this lives in a Zustand store on the client today, which means it does not survive
/// a reinstall and never reaches a second device. A member who sets their location on their
/// phone and opens the web app is asked again. Persisting it is not a new feature so much as
/// finishing an existing one.
/// </para>
/// <para>
/// <b>Travel mode</b> is the subtlety worth stating. A member visiting family abroad wants
/// discovery scoped to where they are standing without losing where they live — so the
/// profile holds two locations and <see cref="ActiveLocation"/> decides which one every query
/// uses. That mirrors the client's <c>getActiveLocation</c> selector exactly, and the
/// selector is the reason the rule belongs in one place rather than at every call site.
/// </para>
/// </summary>
public sealed class LocationProfile : AggregateRoot<LocationProfileId>, IAuditableEntity
{
    /// <summary>Matches the client's <c>slice(0, 5)</c> on the recents list.</summary>
    public const int MaxRecentLocations = 5;

    /// <summary>
    /// Generous enough that no real member hits it, low enough that a scripted client cannot
    /// turn a profile into an unbounded table.
    /// </summary>
    public const int MaxSavedLocations = 25;

    private readonly List<SavedLocation> _savedLocations = [];
    private readonly List<RecentLocation> _recentLocations = [];

    public UserId UserId { get; private set; } = default!;

    /// <summary>
    /// Where the member is discovering from when travel mode is off. Null until they have
    /// either granted location access or chosen somewhere.
    /// </summary>
    public LocationSnapshot? CurrentLocation { get; private set; }

    /// <summary>Where they are while travel mode is on.</summary>
    public LocationSnapshot? TravelLocation { get; private set; }

    public bool TravelMode { get; private set; }

    /// <summary>
    /// The country the member is from. Set at registration and rarely changed; the
    /// eligibility rule that origin must differ from residence is enforced by Identity, not
    /// here, because it is a rule about who may join rather than about where places are.
    /// </summary>
    public CountryCode? OriginCountry { get; private set; }

    /// <summary>The country the member lives in.</summary>
    public CountryCode? HomeCountry { get; private set; }

    public GeographicPreference Preferences { get; private set; } = GeographicPreference.Default;

    public LocationPermissionStatus PermissionStatus { get; private set; } =
        LocationPermissionStatus.Undetermined;

    public IReadOnlyList<SavedLocation> SavedLocations => _savedLocations.AsReadOnly();

    /// <summary>
    /// In no particular order — this is the persisted collection, mapped through its backing
    /// field. Callers rendering the selector want <see cref="RecentLocationsByRecency"/>.
    /// </summary>
    public IReadOnlyList<RecentLocation> RecentLocations => _recentLocations.AsReadOnly();

    /// <summary>Most recent first. What the location selector's "Recent Searches" list shows.</summary>
    public IReadOnlyList<RecentLocation> RecentLocationsByRecency =>
        _recentLocations.OrderByDescending(recent => recent.VisitedAt).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private LocationProfile() { }

    private LocationProfile(LocationProfileId id, UserId userId) : base(id)
    {
        UserId = userId;
    }

    public static LocationProfile CreateFor(UserId userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var profile = new LocationProfile(LocationProfileId.New(), userId);

        profile.RaiseDomainEvent(new LocationProfileCreatedDomainEvent
        {
            ProfileId = profile.Id,
            UserId = userId
        });

        return profile;
    }

    /// <summary>
    /// The location every discovery query in the platform should actually use.
    /// <para>
    /// Travel mode wins when it is on <em>and</em> has somewhere to point at — a member who
    /// toggled travel mode without yet choosing a destination should keep seeing their home
    /// results rather than nothing at all.
    /// </para>
    /// </summary>
    public LocationSnapshot? ActiveLocation =>
        TravelMode && TravelLocation is not null ? TravelLocation : CurrentLocation;

    /// <summary>
    /// Moves the member. Writes through to the travel location while travel mode is on, so
    /// picking a place from the selector does not silently overwrite where they live.
    /// </summary>
    public Result SetCurrentLocation(LocationSnapshot location, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(location);

        var previous = ActiveLocation;

        if (TravelMode)
        {
            TravelLocation = location;
        }
        else
        {
            CurrentLocation = location;
            HomeCountry ??= location.CountryCode;
        }

        RecordRecent(location, at);

        if (previous is null || !previous.SamePlaceAs(location))
        {
            RaiseDomainEvent(new CurrentLocationChangedDomainEvent
            {
                ProfileId = Id,
                UserId = UserId,
                CountryCode = location.CountryCode.Value,
                AreaId = location.AreaId,
                DisplayName = location.DisplayName,
                TravelMode = TravelMode
            });
        }

        return Result.Success();
    }

    /// <summary>
    /// Turns travel mode on or off. Turning it on does not clear the previous destination —
    /// the member who toggles it off and on again while abroad expects to be where they were.
    /// </summary>
    public void SetTravelMode(bool enabled, LocationSnapshot? destination, DateTimeOffset at)
    {
        TravelMode = enabled;

        if (!enabled)
        {
            return;
        }

        if (destination is not null)
        {
            TravelLocation = destination;
            RecordRecent(destination, at);
        }
    }

    public void SetPermissionStatus(LocationPermissionStatus status) => PermissionStatus = status;

    public void SetOriginCountry(CountryCode? country) => OriginCountry = country;

    public void SetHomeCountry(CountryCode? country) => HomeCountry = country;

    public void UpdatePreferences(GeographicPreference preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Preferences = preferences;
    }

    /// <summary>
    /// Saves or moves a labelled place.
    /// <para>
    /// Idempotent on the label: saving "Home" twice moves it rather than creating a second
    /// one, which is the behaviour the client's <c>Record</c>-keyed store already has.
    /// </para>
    /// </summary>
    public Result<SavedLocation> SaveLocation(string label, LocationSnapshot location, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure<SavedLocation>(Error.Validation(
                LocationErrorCodes.SavedLocationNotFound, "A label is required."));
        }

        ArgumentNullException.ThrowIfNull(location);

        var existing = FindSaved(label);

        if (existing is not null)
        {
            existing.MoveTo(location, at);
            return Result.Success(existing);
        }

        if (_savedLocations.Count >= MaxSavedLocations)
        {
            return Result.Failure<SavedLocation>(Error.Validation(
                LocationErrorCodes.SavedLocationLimitReached,
                $"You can save at most {MaxSavedLocations} locations."));
        }

        var saved = SavedLocation.Create(Id, label, location, at);
        _savedLocations.Add(saved);

        RaiseDomainEvent(new SavedLocationChangedDomainEvent
        {
            ProfileId = Id,
            UserId = UserId,
            Label = saved.Label,
            Removed = false
        });

        return Result.Success(saved);
    }

    public Result RemoveSavedLocation(string label)
    {
        var existing = FindSaved(label);

        if (existing is null)
        {
            return Result.Failure(Error.NotFound(
                LocationErrorCodes.SavedLocationNotFound, $"No location is saved as '{label}'."));
        }

        _savedLocations.Remove(existing);

        RaiseDomainEvent(new SavedLocationChangedDomainEvent
        {
            ProfileId = Id,
            UserId = UserId,
            Label = existing.Label,
            Removed = true
        });

        return Result.Success();
    }

    public void ClearRecentLocations() => _recentLocations.Clear();

    /// <summary>
    /// Pushes a place onto the recents list, de-duplicating and trimming to the cap.
    /// <para>
    /// Mirrors the client's own reducer: filter out the same place, unshift, take five. The
    /// de-duplication is by place rather than by identifier, because the same neighbourhood
    /// reached by GPS and by search produces two snapshots and one entry is what the member
    /// expects to see.
    /// </para>
    /// </summary>
    private void RecordRecent(LocationSnapshot location, DateTimeOffset at)
    {
        var duplicate = _recentLocations.FirstOrDefault(
            recent => recent.Location.SamePlaceAs(location));

        if (duplicate is not null)
        {
            duplicate.Touch(at);
        }
        else
        {
            _recentLocations.Add(RecentLocation.Create(Id, location, at));
        }

        if (_recentLocations.Count <= MaxRecentLocations)
        {
            return;
        }

        foreach (var stale in _recentLocations
                     .OrderByDescending(recent => recent.VisitedAt)
                     .Skip(MaxRecentLocations)
                     .ToList())
        {
            _recentLocations.Remove(stale);
        }
    }

    private SavedLocation? FindSaved(string label) =>
        _savedLocations.FirstOrDefault(
            saved => string.Equals(saved.Label, label.Trim(), StringComparison.OrdinalIgnoreCase));

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
