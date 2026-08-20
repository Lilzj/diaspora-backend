using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Verification;

/// <summary>
/// Bounds the evidence a claim may carry.
/// <para>
/// The ceiling is what a human reviewer can reasonably open in one sitting; past it, a
/// submission stops being evidence and becomes a denial of the reviewer's attention. The
/// document type is deliberately <em>not</em> required here — omitted, the shipped provider
/// supplies the type the deciding system accepts.
/// </para>
/// </summary>
public sealed class SubmitGuideVerificationCommandValidator
    : AbstractValidator<SubmitGuideVerificationCommand>
{
    /// <summary>Pieces of evidence one claim may carry.</summary>
    public const int MaxEvidenceItems = 20;

    /// <summary>Characters of free text a reviewer is asked to read.</summary>
    public const int MaxNotes = 2_000;

    public SubmitGuideVerificationCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Evidence)
                .NotNull()
                .Must(evidence => evidence.Count <= MaxEvidenceItems)
                .WithMessage($"At most {MaxEvidenceItems} pieces of evidence.");

            RuleForEach(command => command.Request.Evidence)
                .ChildRules(evidence => evidence.RuleFor(input => input.MediaId).NotEmpty());

            RuleFor(command => command.Request.Notes).MaximumLength(MaxNotes);
        });
    }
}
