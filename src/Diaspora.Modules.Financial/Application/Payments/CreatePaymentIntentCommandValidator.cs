using FluentValidation;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// What the platform refuses before a gateway ever sees it.
/// <para>
/// The ceiling is a domain limit rather than a business one: it is far above any legitimate
/// transaction and far below the point at which summing a lifetime of them could overflow a 64-bit
/// minor-unit total.
/// </para>
/// </summary>
public sealed class CreatePaymentIntentCommandValidator
    : AbstractValidator<CreatePaymentIntentCommand>
{
    public CreatePaymentIntentCommandValidator()
    {
        RuleFor(command => command.Amount)
            .GreaterThan(0m)
            .WithMessage("A payment for nothing is not a payment.");

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("A currency is three ISO 4217 letters.");

        RuleFor(command => command.MethodKind).NotEmpty();

        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(Domain.Policies.FinancialLimits.MaxDescriptionLength);

        RuleFor(command => command.IdempotencyKey)
            .MaximumLength(Domain.Policies.FinancialLimits.MaxReferenceLength);
    }
}
