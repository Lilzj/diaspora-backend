using Diaspora.Modules.Housing.Extensibility.Catalog;

namespace Diaspora.Modules.Housing.Infrastructure.Catalog;

/// <summary>
/// The accommodation types the platform ships with.
/// <para>
/// <b>Every type the B11 brief names, and nothing beyond them.</b> A deployment operating in a
/// market with a category this list does not cover — a riad, a boys' quarters, a co-living
/// suite — ships its own contributor rather than editing this one, which is the whole reason the
/// catalogue is a table.
/// </para>
/// <para>
/// <b>The two behaviour flags are the ones worth reading carefully.</b> <c>IsShared</c> permits
/// roommate preferences and a gender preference on listings of that type, and is set only where
/// somebody else genuinely lives there. <c>IsTemporary</c> is what a member with six weeks before
/// their permanent move filters on, and is set on the four types that are somewhere to stay rather
/// than somewhere to live.
/// </para>
/// </summary>
public sealed class DefaultAccommodationTypeContributor : IAccommodationTypeContributor
{
    public IEnumerable<AccommodationTypeSeed> GetAccommodationTypes()
    {
        // ── Whole homes ─────────────────────────────────────────────────────────────────────
        yield return Type("apartment", "Apartment", 10, "A self-contained flat.");
        yield return Type("studio", "Studio", 20, "One room, with its own kitchen and bathroom.");
        yield return Type("house", "House", 30, "A whole house.");
        yield return Type("duplex", "Duplex", 40, "A home on two floors, or half of a pair.");
        yield return Type("villa", "Villa", 50, "A detached house, usually with grounds.");

        // ── Shared. IsShared permits roommate matching and a gender preference. ─────────────
        yield return new AccommodationTypeSeed
        {
            Key = "shared-apartment",
            Name = "Shared Apartment",
            Description = "A flat shared with other tenants.",
            IsShared = true,
            Order = 60
        };

        yield return new AccommodationTypeSeed
        {
            Key = "private-room",
            Name = "Private Room",
            Description = "Your own room in a shared home.",
            IsShared = true,
            Order = 70
        };

        yield return new AccommodationTypeSeed
        {
            Key = "shared-room",
            Name = "Shared Room",
            Description = "A room shared with somebody else.",
            IsShared = true,
            Order = 80
        };

        yield return new AccommodationTypeSeed
        {
            Key = "roommate-wanted",
            Name = "Roommate Wanted",
            Description = "An existing household looking for somebody to join it.",
            IsShared = true,
            Order = 90
        };

        yield return new AccommodationTypeSeed
        {
            Key = "family-stay",
            Name = "Family Stay",
            Description = "A room with a host family.",
            IsShared = true,
            IsTemporary = true,
            Order = 100
        };

        // ── Temporary. What somebody filters on before their permanent move. ────────────────
        yield return new AccommodationTypeSeed
        {
            Key = "hostel",
            Name = "Hostel",
            Description = "A bed in shared accommodation, by the night or the week.",
            IsShared = true,
            IsTemporary = true,
            Order = 110
        };

        yield return new AccommodationTypeSeed
        {
            Key = "student-hostel",
            Name = "Student Hostel",
            Description = "Purpose-built student accommodation.",
            IsShared = true,
            Order = 120
        };

        yield return new AccommodationTypeSeed
        {
            Key = "guest-house",
            Name = "Guest House",
            Description = "A small guest house, let by the room.",
            IsTemporary = true,
            Order = 130
        };

        yield return new AccommodationTypeSeed
        {
            Key = "temporary-accommodation",
            Name = "Temporary Accommodation",
            Description = "Somewhere to stay while you find somewhere to live.",
            IsTemporary = true,
            Order = 140
        };

        yield return new AccommodationTypeSeed
        {
            Key = "hotel-partnership",
            Name = "Hotel",
            Description = "A hotel room on relocation terms.",
            IsTemporary = true,
            Order = 150
        };

        yield return new AccommodationTypeSeed
        {
            Key = "company-accommodation",
            Name = "Company Accommodation",
            Description = "Housing provided by an employer.",
            Order = 160
        };
    }

    private static AccommodationTypeSeed Type(
        string key, string name, int order, string description) => new()
    {
        Key = key,
        Name = name,
        Description = description,
        Order = order
    };
}
