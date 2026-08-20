namespace Diaspora.Modules.CommunityLife.Domain.Rsvps;

/// <summary>
/// The true place counts for one event, summed over party sizes.
/// <para>
/// A record rather than four out-parameters, and summed rather than counted: a count of rows is
/// wrong the moment anybody brings their family, which on this platform is most of them.
/// </para>
/// </summary>
public sealed record RsvpPlaceTotals(
    int Going, int Interested, int Waitlisted, int CheckedIn)
{
    public static RsvpPlaceTotals Empty { get; } = new(0, 0, 0, 0);
}
