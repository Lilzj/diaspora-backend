using Diaspora.Modules.Media.Application.Uploads;
using FluentValidation;

namespace Diaspora.Modules.Media.Application.Validation;

/// <summary>
/// Shape checks for opening an upload.
/// <para>
/// Deliberately only the shape. Whether a content type is <em>allowed</em>, whether a size is
/// within the ceiling for its kind, and whether the bytes match their declaration are domain
/// decisions with domain error codes, and duplicating them here would give the same rejection
/// two different codes depending on which layer caught it first. A validator that says "this
/// field is missing" and a domain that says "that file is 18.4 MB; the limit is 15 MB" are
/// answering different questions.
/// </para>
/// </summary>
public sealed class StartUploadValidator : AbstractValidator<StartUploadCommand>
{
    public StartUploadValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Kind)
            .NotEmpty()
            .WithMessage("Say what kind of file this is.");

        RuleFor(command => command.Request.ContentType)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(command => command.Request.FileName)
            .NotEmpty()
            .MaximumLength(260);

        RuleFor(command => command.Request.SizeBytes)
            .GreaterThan(0)
            .WithMessage("A file with no bytes cannot be uploaded.");

        // 64 lower-case hex characters, when supplied at all. Checked here because a
        // malformed digest is a client bug rather than a policy decision, and failing it early
        // saves opening a session that can never complete.
        RuleFor(command => command.Request.ContentHash)
            .Matches("^[a-fA-F0-9]{64}$")
            .When(command => !string.IsNullOrWhiteSpace(command.Request.ContentHash))
            .WithMessage("A content hash must be a 64-character SHA-256 digest.");

        // An owner is optional, but half of one is not: a type without an identifier is a
        // caller who believes they attached the upload to something and has not.
        RuleFor(command => command.Request.OwnerId)
            .NotNull()
            .When(command => !string.IsNullOrWhiteSpace(command.Request.OwnerType))
            .WithMessage("An owner type needs the identifier of the thing that owns the file.");

        RuleFor(command => command.Request.OwnerType)
            .NotEmpty()
            .When(command => command.Request.OwnerId is not null)
            .WithMessage("An owner identifier needs an owner type.");

        RuleFor(command => command.Request.Role)
            .MaximumLength(64);
    }
}
