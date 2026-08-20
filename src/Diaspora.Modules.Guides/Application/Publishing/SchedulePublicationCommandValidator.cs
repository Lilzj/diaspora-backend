using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>
/// Checks that an instant was supplied at all.
/// <para>
/// Whether it is in the <em>future</em> is not asked here: "future" is relative to the platform
/// clock, and a validator that read a clock would disagree with the aggregate about the boundary
/// under load. The aggregate owns that comparison and returns
/// <c>GuideErrors.ScheduleInPast</c>.
/// </para>
/// </summary>
public sealed class SchedulePublicationCommandValidator
    : AbstractValidator<SchedulePublicationCommand>
{
    public SchedulePublicationCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.PublishAt)
            .NotEqual(default(DateTimeOffset))
            .When(command => command.Request is not null);
    }
}
