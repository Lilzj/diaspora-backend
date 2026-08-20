using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Bounds the one number a client can get wrong at scale.
/// <para>
/// A day of reading seconds is far past any real session and comfortably past a tab left open
/// over lunch; past it, the number is a bug or a script, and averaging it into the author's
/// read-time report would poison the statistic for everyone.
/// </para>
/// </summary>
public sealed class RecordGuideViewCommandValidator : AbstractValidator<RecordGuideViewCommand>
{
    /// <summary>Seconds one reported read may claim.</summary>
    public const long MaxReadSeconds = 86_400;

    public RecordGuideViewCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.ReadSeconds)
            .InclusiveBetween(0, MaxReadSeconds)
            .When(command => command.Request is { ReadSeconds: not null });
    }
}
