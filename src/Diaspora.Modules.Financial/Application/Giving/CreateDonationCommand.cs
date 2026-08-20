using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>
/// Gives money.
/// <para>
/// <b>Transactional: a donation writes the donation and a payment intent.</b> A pledged donation
/// with no payment behind it would sit pending for ever, counted by nothing and chased by nobody.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "A donation writes the donation record and its payment intent together.")]
[Auditable(Action = "financial.donation.create")]
public sealed record CreateDonationCommand : ICommand<DonationResponse>
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public string? CauseCode { get; init; }

    public string? BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public bool IsAnonymous { get; init; }

    public string? Message { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }

    public int? RecurringIntervalMonths { get; init; }
}
