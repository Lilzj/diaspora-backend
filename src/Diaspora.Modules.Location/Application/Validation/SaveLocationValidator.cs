using Diaspora.Modules.Location.Application.Profile;
using Diaspora.Modules.Location.Domain.Profiles;
using FluentValidation;

namespace Diaspora.Modules.Location.Application.Validation;

/// <summary>Shape validation for saving a labelled place.</summary>
public sealed class SaveLocationValidator : AbstractValidator<SaveLocationCommand>
{
    public SaveLocationValidator()
    {
        RuleFor(command => command.Label)
            .NotEmpty()
            .MaximumLength(SavedLocation.MaxLabelLength);
    }
}
