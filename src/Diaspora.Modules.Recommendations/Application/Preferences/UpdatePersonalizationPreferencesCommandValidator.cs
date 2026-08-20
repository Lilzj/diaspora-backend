using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Modules.Recommendations.Domain.Enums;
using FluentValidation;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>What a member may state.</summary>
public sealed class UpdatePersonalizationPreferencesCommandValidator
    : AbstractValidator<UpdatePersonalizationPreferencesCommand>
{
    public UpdatePersonalizationPreferencesCommandValidator()
    {
        RuleFor(command => command.PreferredLanguage).MaximumLength(16);
        RuleFor(command => command.PreferredCountryCode).MaximumLength(8);

        RuleForEach(command => command.Categories!)
            .SetValidator(new CategoryPreferenceRequestValidator())
            .When(command => command.Categories is not null);

        RuleForEach(command => command.Surfaces!)
            .SetValidator(new SurfacePreferenceRequestValidator())
            .When(command => command.Surfaces is not null);

        RuleForEach(command => command.FavoriteLocations!)
            .SetValidator(new FavoriteLocationRequestValidator())
            .When(command => command.FavoriteLocations is not null);
    }
}

/// <summary>One category preference.</summary>
public sealed class CategoryPreferenceRequestValidator : AbstractValidator<CategoryPreferenceRequest>
{
    public CategoryPreferenceRequestValidator()
    {
        RuleFor(request => request.CategoryKey).NotEmpty().MaximumLength(128);

        RuleFor(request => request.TargetType)
            .Must(type => type is null ||
                          Enum.TryParse<RecommendationTargetType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a recommendation target type.");
    }
}

/// <summary>One surface preference.</summary>
public sealed class SurfacePreferenceRequestValidator : AbstractValidator<SurfacePreferenceRequest>
{
    public SurfacePreferenceRequestValidator() =>
        RuleFor(request => request.Surface)
            .NotEmpty()
            .Must(surface => Enum.TryParse<RecommendationSurface>(surface, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a recommendation surface.");
}

/// <summary>One favourite place.</summary>
public sealed class FavoriteLocationRequestValidator : AbstractValidator<FavoriteLocationRequest>
{
    public FavoriteLocationRequestValidator()
    {
        RuleFor(request => request.Key).NotEmpty().MaximumLength(128);

        RuleFor(request => request.Scope)
            .NotEmpty()
            .Must(scope => Enum.TryParse<AffinityScope>(scope, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a location scope.");
    }
}
