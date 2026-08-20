using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Referencing;

/// <summary>Bounds the reference list and the editorial text on each entry.</summary>
public sealed class SetGuideProviderReferencesCommandValidator
    : AbstractValidator<SetGuideProviderReferencesCommand>
{
    public SetGuideProviderReferencesCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.References)
                .NotNull()
                .Must(references => references.Count <= GuideLimits.MaxProviderReferences)
                .WithMessage(
                    $"A guide may reference at most {GuideLimits.MaxProviderReferences} providers.");

            RuleForEach(command => command.Request.References)
                .ChildRules(reference =>
                {
                    reference.RuleFor(input => input.ProviderId).NotEmpty();

                    reference.RuleFor(input => input.Role)
                        .MaximumLength(GuideLimits.MaxProviderReferenceRole);

                    reference.RuleFor(input => input.Note)
                        .MaximumLength(GuideLimits.MaxProviderReferenceNote);
                });
        });
    }
}
