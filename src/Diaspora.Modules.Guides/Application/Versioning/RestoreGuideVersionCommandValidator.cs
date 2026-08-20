using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// Bounds the route values. Version numbers start at one — a zero is a client that has confused
/// "unpublished" with "version zero", and the repository would answer not-found either way.
/// </summary>
public sealed class RestoreGuideVersionCommandValidator
    : AbstractValidator<RestoreGuideVersionCommand>
{
    public RestoreGuideVersionCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.VersionNumber).GreaterThan(0);
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.BaseRevision)
            .GreaterThanOrEqualTo(0)
            .When(command => command.Request is { BaseRevision: not null });
    }
}
