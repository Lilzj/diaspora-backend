using FluentValidation;

namespace Diaspora.Modules.Recommendations.Application.Strategies;

/// <summary>
/// What an operator may set.
/// <para>
/// The aggregate clamps these as well. Both exist because they fail at different times: the validator
/// tells an operator their weight of 1.5 was rejected, and the aggregate makes sure a weight of 1.5
/// arriving through any other path becomes 1 rather than out-weighing every other strategy on the
/// surface by half.
/// </para>
/// </summary>
public sealed class UpdateRankingStrategyCommandValidator
    : AbstractValidator<UpdateRankingStrategyCommand>
{
    public UpdateRankingStrategyCommandValidator()
    {
        RuleFor(command => command.StrategyId).NotEmpty();

        RuleFor(command => command.Weight)
            .InclusiveBetween(0m, 1m)
            .When(command => command.Weight is not null)
            .WithMessage("A strategy weight is between 0 and 1.");

        RuleFor(command => command.MinimumScore)
            .InclusiveBetween(0m, 1m)
            .When(command => command.MinimumScore is not null);

        RuleFor(command => command.Description).MaximumLength(512);
    }
}
