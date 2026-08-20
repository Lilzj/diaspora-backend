using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Application.Signals;

/// <summary>
/// What a client may submit.
/// <para>
/// <b>The batch ceiling is the load-bearing rule.</b> This is the platform's highest-volume write
/// endpoint and the only one an ordinary member can call in a loop; without a ceiling, a modified
/// client could insert unbounded rows into the largest table in the schema in a single request.
/// </para>
/// <para>
/// <b>Nothing here validates the weight, and that is deliberate.</b> A weight a client claims is
/// clamped by the signal store against what its source is trusted with — a rule that belongs with
/// the enforcement rather than with the request shape, because a second write path would bypass a
/// validator and not the store.
/// </para>
/// </summary>
public sealed class RecordSignalsCommandValidator : AbstractValidator<RecordSignalsCommand>
{
    public RecordSignalsCommandValidator(IOptions<RecommendationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(command => command.Signals)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one signal is required.");

        RuleFor(command => command.Signals.Count)
            .LessThanOrEqualTo(options.Value.MaximumSignalBatchSize)
            .WithMessage(
                $"A batch may contain at most {options.Value.MaximumSignalBatchSize} signals.");

        RuleForEach(command => command.Signals).SetValidator(new RecordSignalRequestValidator());
    }
}

/// <summary>One signal in a batch.</summary>
public sealed class RecordSignalRequestValidator : AbstractValidator<RecordSignalRequest>
{
    public RecordSignalRequestValidator()
    {
        RuleFor(signal => signal.Kind)
            .NotEmpty()
            .Must(kind => Enum.TryParse<SignalKind>(kind, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a signal kind.");

        RuleFor(signal => signal.TargetType)
            .Must(type => type is null ||
                          Enum.TryParse<RecommendationTargetType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a recommendation target type.");

        RuleFor(signal => signal.KeyKind)
            .Must(kind => kind is null || Enum.TryParse<InterestKind>(kind, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not an interest kind.");

        RuleFor(signal => signal.Surface)
            .Must(surface => surface is null ||
                             Enum.TryParse<RecommendationSurface>(surface, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a recommendation surface.");

        RuleFor(signal => signal.Key).MaximumLength(128);
        RuleFor(signal => signal.CountryCode).MaximumLength(8);
        RuleFor(signal => signal.CityName).MaximumLength(128);
        RuleFor(signal => signal.NeighborhoodName).MaximumLength(128);

        // A signal dated in the future would decay backwards — its effective weight would exceed its
        // recorded one — and a client with a wrong clock is common enough to guard against.
        RuleFor(signal => signal.OccurredAt)
            .Must(occurredAt => occurredAt is null || occurredAt <= DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("A signal cannot have happened in the future.");
    }
}
