namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Replaces a provider's opening hours, a week at a time.
/// <para>
/// <see cref="OperatingHoursRequest"/> is deliberately per-day — a person edits Tuesday, not
/// "the week" — but the client's hours editor submits the whole grid at once, and seven round
/// trips to save one screen is seven chances to half-succeed. So the wire shape is a list and
/// the domain call stays per-day: <c>SetDayHours</c> is invoked once per entry inside a single
/// transaction, and either the whole week lands or none of it does.
/// </para>
/// <para>
/// <b>Days omitted from <see cref="Hours"/> are left alone.</b> Sending Monday does not silently
/// close Tuesday. Clearing a day is <see cref="OperatingHoursRequest.IsClosed"/>, which is a
/// statement ("we do not open Sundays"); absence is the absence of a statement, and the two must
/// not collapse into each other.
/// </para>
/// <para>
/// Mirrors <c>CommunityLife</c>'s <c>SetOperatingHoursRequest</c>, which solved the same problem
/// for organizations. Same shape, same reasoning, so the two hours editors on this platform do
/// not need to be understood separately.
/// </para>
/// </summary>
public sealed record SetProviderOperatingHoursRequest
{
    /// <summary>
    /// One entry per day being set. A duplicate day is refused rather than last-one-wins: two
    /// contradictory instructions in one payload is a client bug, and silently honouring one of
    /// them hides it.
    /// </summary>
    public IReadOnlyList<OperatingHoursRequest> Hours { get; init; } = [];
}
