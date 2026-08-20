using Diaspora.Modules.Media.Application.Assets;
using Diaspora.Modules.Media.Domain.Assets;
using FluentValidation;

namespace Diaspora.Modules.Media.Application.Validation;

/// <summary>
/// Length checks for a description change.
/// <para>
/// The limits are the aggregate's own constants rather than repeated numbers, so a validator
/// that permits more than the column holds cannot exist — which is the failure that produces
/// silent truncation.
/// </para>
/// </summary>
public sealed class UpdateMediaValidator : AbstractValidator<UpdateMediaCommand>
{
    public UpdateMediaValidator()
    {
        RuleFor(command => command.MediaId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.AltText)
            .MaximumLength(MediaAsset.MaxAltTextLength);

        RuleFor(command => command.Request.Caption)
            .MaximumLength(MediaAsset.MaxCaptionLength);
    }
}
