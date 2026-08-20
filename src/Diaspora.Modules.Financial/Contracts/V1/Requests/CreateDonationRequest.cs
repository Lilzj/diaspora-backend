namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Give money.
/// <para>
/// <b>Setting <see cref="IsAnonymous"/> means the donor identifier is not stored at all</b> — not
/// hidden behind a flag. The consequence is stated in the response and worth repeating: an anonymous
/// donation cannot appear in the donor's own history.
/// </para>
/// </summary>
public sealed record CreateDonationRequest
{
    /// <summary>Major units.</summary>
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>The cause code, when giving to a listed cause.</summary>
    public string? CauseCode { get; init; }

    /// <summary>
    /// <c>Member</c>, <c>Organization</c>, <c>Community</c>, <c>Fund</c> or <c>Platform</c>.
    /// Required when no cause is named.
    /// </summary>
    public string? BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public bool IsAnonymous { get; init; }

    public string? Message { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Declared and not scheduled in this phase. Stored so the column, the contract and the client
    /// field all exist from the start.
    /// </summary>
    public int? RecurringIntervalMonths { get; init; }
}
