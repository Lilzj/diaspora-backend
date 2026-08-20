using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Rejects a creation the domain would refuse anyway, before a database round trip.
/// <para>
/// Shape only. Whether the category key names a real, active category and whether the slug is
/// free are catalogue and uniqueness questions the handler asks — a validator that answered them
/// would need repositories and would answer them again a moment later.
/// </para>
/// </summary>
public sealed class CreateGuideCommandValidator : AbstractValidator<CreateGuideCommand>
{
    public CreateGuideCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Title)
                .NotEmpty()
                .MinimumLength(GuideLimits.MinTitle)
                .MaximumLength(GuideLimits.MaxTitle);

            RuleFor(command => command.Request.CategoryKey)
                .NotEmpty()
                .MaximumLength(GuideLimits.MaxCategoryKey);

            RuleFor(command => command.Request.Language)
                .NotEmpty()
                .Length(2);

            RuleFor(command => command.Request.CountryCode)
                .NotEmpty()
                .Length(2);

            RuleFor(command => command.Request.Slug)
                .MaximumLength(GuideLimits.MaxSlugLength);

            RuleFor(command => command.Request.Summary)
                .MaximumLength(GuideLimits.MaxSummary);

            RuleFor(command => command.Request.RegionName)
                .MaximumLength(GuideLimits.MaxLocationName);

            RuleFor(command => command.Request.CityName)
                .MaximumLength(GuideLimits.MaxLocationName);
        });
    }
}
