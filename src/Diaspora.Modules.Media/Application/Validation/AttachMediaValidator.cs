using Diaspora.Modules.Media.Application.Assets;
using FluentValidation;

namespace Diaspora.Modules.Media.Application.Validation;

/// <summary>Shape checks for claiming an asset.</summary>
public sealed class AttachMediaValidator : AbstractValidator<AttachMediaCommand>
{
    public AttachMediaValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.MediaId).NotEmpty();

        RuleFor(command => command.Request.OwnerType)
            .NotEmpty()
            .WithMessage("Say what kind of thing owns this file.");

        RuleFor(command => command.Request.OwnerId)
            .NotEmpty()
            .WithMessage("Say which one.");

        RuleFor(command => command.Request.Role)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Say what the file is to its owner: cover, gallery, avatar.");

        RuleFor(command => command.Request.Position)
            .GreaterThanOrEqualTo(0);
    }
}
