using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Checks that a reason was given at all.
/// <para>
/// Which reasons are legal is Community's catalogue, parsed in the one class that knows its
/// vocabulary — an unrecognised reason files as "other" with the description carried verbatim,
/// so a member reporting through an older client is never refused over a renamed enum member.
/// </para>
/// </summary>
public sealed class ReportGuideCommandValidator : AbstractValidator<ReportGuideCommand>
{
    /// <summary>Characters of description a moderator is asked to read.</summary>
    public const int MaxDescription = 2_000;

    public ReportGuideCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Reason).NotEmpty();
            RuleFor(command => command.Request.Description).MaximumLength(MaxDescription);
        });
    }
}
