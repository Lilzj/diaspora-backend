namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// Something that can be given to.
/// <para>
/// Every field here answers one the client's donation-causes screen already renders against a mock:
/// organiser and organiser type, urgency, supporter count, verification badge, location, contact
/// numbers, and the accepted support kinds.
/// </para>
/// <para>
/// <b><see cref="Raised"/> is null when the cause takes no money.</b> A coat drive that has gathered
/// forty volunteers and no cash is a success, and a zero would read as a failure.
/// </para>
/// </summary>
public sealed record DonationCauseResponse
{
    public required Guid CauseId { get; init; }

    public required string Code { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public required string OrganizerName { get; init; }

    /// <summary><c>Urgent</c>, <c>High</c>, <c>Moderate</c> or <c>Ongoing</c>.</summary>
    public required string Urgency { get; init; }

    /// <summary>
    /// <c>Monetary</c>, <c>Goods</c>, <c>VolunteerService</c>, <c>ProfessionalSkills</c>,
    /// <c>Supplies</c>. Most causes take more than one.
    /// </summary>
    public required IReadOnlyList<string> AcceptedKinds { get; init; }

    public MoneyResponse? Raised { get; init; }

    public MoneyResponse? Target { get; init; }

    /// <summary>Everybody who gave anything, monetary or not.</summary>
    public required int SupporterCount { get; init; }

    public required bool IsVerified { get; init; }

    public required bool IsActive { get; init; }

    public string? CountryCode { get; init; }

    public string? LocationLabel { get; init; }

    public string? ContactPhone { get; init; }

    public string? ContactWhatsapp { get; init; }

    /// <summary>A Media reference, never a URL. Resolve it through Media as on every other surface.</summary>
    public string? MediaReference { get; init; }

    /// <summary>
    /// When the cause was opened.
    /// <para>
    /// Added in B15.1 because the client's card wanted "posted 2 days ago" and the response carried
    /// no timestamp of any kind, so the line was removed rather than filled with a plausible date. A
    /// missing field is better than an invented one, and a real field is better than both.
    /// </para>
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
