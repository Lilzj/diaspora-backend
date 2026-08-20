using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// One row of an organization's opening hours.
/// <para>
/// <b>Times of day rather than instants, and a note that can say anything.</b> The times are local
/// to the organization and mean the same thing all year, which an instant would not. The note is
/// what carries "Jumu'ah 13:15" or "second Sunday only" — the qualifications that make published
/// hours honest and that no schema of times could hold.
/// </para>
/// <para>
/// A row with <see cref="IsClosed"/> set is not the absence of a row. "Closed on Mondays" is
/// information; a missing Monday is silence, and the client renders the two differently.
/// </para>
/// </summary>
public sealed class OrganizationOperatingHour : Entity<Guid>
{
    private OrganizationOperatingHour(
        Guid id, OperatingDay day, TimeOnly? opensAt, TimeOnly? closesAt, bool isClosed, string? note)
        : base(id)
    {
        Day = day;
        OpensAt = opensAt;
        ClosesAt = closesAt;
        IsClosed = isClosed;
        Note = note;
    }

    private OrganizationOperatingHour() { }

    public OperatingDay Day { get; private set; }

    public TimeOnly? OpensAt { get; private set; }

    public TimeOnly? ClosesAt { get; private set; }

    /// <summary>Explicitly shut. Different from having no row for the day.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The qualification. Where the real answer usually lives.</summary>
    public string? Note { get; private set; }

    public static OrganizationOperatingHour Create(
        Guid id,
        OperatingDay day,
        TimeOnly? opensAt = null,
        TimeOnly? closesAt = null,
        bool isClosed = false,
        string? note = null)
    {
        // A row with no times and no note is a row that says nothing, so it is read as closed.
        var closed = isClosed || (opensAt is null && closesAt is null && string.IsNullOrWhiteSpace(note));

        return new OrganizationOperatingHour(
            id,
            day,
            closed ? null : opensAt,
            closed ? null : closesAt,
            closed,
            Clip(note, CommunityLifeLimits.MaxDisplayName));
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
