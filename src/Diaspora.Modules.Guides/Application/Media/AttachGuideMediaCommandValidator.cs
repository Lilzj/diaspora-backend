using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Media;

/// <summary>
/// Checks that an asset and a role were named. Whether the role is one this module knows is the
/// handler's question — the vocabulary lives in <c>GuideWireNames</c>, and duplicating it here
/// would be a second place for it to drift.
/// </summary>
public sealed class AttachGuideMediaCommandValidator : AbstractValidator<AttachGuideMediaCommand>
{
    public AttachGuideMediaCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.MediaId).NotEmpty();
            RuleFor(command => command.Request.Role).NotEmpty();

            RuleFor(command => command.Request.Caption)
                .MaximumLength(GuideLimits.MaxMediaCaption);
        });
    }
}
