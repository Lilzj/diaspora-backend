using Diaspora.Modules.Media.Application.Assets;
using Diaspora.Modules.Media.Domain.Assets;
using FluentValidation;

namespace Diaspora.Modules.Media.Application.Validation;

/// <summary>
/// A moderation decision names its action and carries a reason the column can hold.
/// </summary>
public sealed class ModerateMediaValidator : AbstractValidator<ModerateMediaCommand>
{
    public ModerateMediaValidator()
    {
        RuleFor(command => command.MediaId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Action).NotEmpty();

        RuleFor(command => command.Request.Reason)
            .NotEmpty()
            .WithMessage("A file is taken down with a reason, or not at all.")
            .MaximumLength(MediaAsset.MaxCaptionLength);
    }
}
