using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// When and where the buyer is meant to collect.
/// <para>
/// <b>A window rather than an appointment.</b> Peer-to-peer hand-offs are arranged loosely —
/// "Saturday afternoon", "any evening this week" — and a single timestamp would force both parties
/// to agree a precision they never agreed. The pair is what a reminder can be sent against and
/// what the expiry sweep measures.
/// </para>
/// <para>
/// <b>The place is free text, and deliberately not a Location entity.</b> "Outside the station",
/// "my office reception" and "the shop" are not rows in B4's hierarchy, and putting them there
/// would pollute a catalogue every other vertical reads. The listing's own location is the
/// authoritative geography; this is the meeting instruction.
/// </para>
/// </summary>
public sealed class CollectionWindow : ValueObject
{
    private CollectionWindow(DateTimeOffset? from, DateTimeOffset? until, string? place)
    {
        From = from;
        Until = until;
        Place = place;
    }

    private CollectionWindow() { }

    public DateTimeOffset? From { get; private set; }

    /// <summary>When the hold lapses. The expiry sweep's work list is ordered by this.</summary>
    public DateTimeOffset? Until { get; private set; }

    /// <summary>The meeting instruction, in the parties' own words.</summary>
    public string? Place { get; private set; }

    /// <summary>Nothing arranged yet. A reservation may legitimately start here.</summary>
    public static CollectionWindow Unscheduled => new(null, null, null);

    /// <summary>Whether the window has closed as of the given instant.</summary>
    public bool HasLapsed(DateTimeOffset now) => Until is { } until && until <= now;

    public static Result<CollectionWindow> Create(
        DateTimeOffset? from, DateTimeOffset? until, string? place)
    {
        if (from is { } start && until is { } end && end <= start)
        {
            return Result.Failure<CollectionWindow>(MarketplaceErrors.ReservationWindowInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(place) ? null : place.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxReservationNote })
        {
            return Result.Failure<CollectionWindow>(MarketplaceErrors.ReservationWindowInvalid);
        }

        return Result.Success(new CollectionWindow(from, until, trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return From;
        yield return Until;
        yield return Place;
    }
}
