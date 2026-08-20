using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// The money is the platform's.
/// <para>
/// The event every downstream grant hangs off: a placement activates on capture, a subscription
/// starts on capture, a donation is receipted on capture. Nothing grants on authorisation, because
/// an authorisation can still fail.
/// </para>
/// </summary>
public sealed record PaymentCapturedDomainEvent : DomainEvent
{
    public required PaymentIntentId PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    /// <summary>
    /// The payer's financial identity. Carried so a deposit can be settled against the wallet on the
    /// intent's own account rather than one resolved again from the payer — which is the difference
    /// between "the account this payment was opened against" and "whichever account this user has
    /// now".
    /// </summary>
    public required FinancialAccountId AccountId { get; init; }

    /// <summary>Where the money landed: the platform's revenue, or the payer's own balance.</summary>
    public required PaymentPurpose Purpose { get; init; }

    public PaymentMethodKind MethodKind { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public MonetizationSubjectType? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? Provider { get; init; }

    public string? ExternalReference { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }
}
