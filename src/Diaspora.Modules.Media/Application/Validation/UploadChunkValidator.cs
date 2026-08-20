using Diaspora.Modules.Media.Application.Uploads;
using FluentValidation;

namespace Diaspora.Modules.Media.Application.Validation;

/// <summary>Shape checks for one received part.</summary>
public sealed class UploadChunkValidator : AbstractValidator<UploadChunkCommand>
{
    public UploadChunkValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();

        // One-based, because the client's parts are numbered the way a person counts them.
        // The upper bound is the session's own chunk count, which only the domain knows.
        RuleFor(command => command.ChunkNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Part numbers start at 1.");

        RuleFor(command => command.Content).NotNull();

        RuleFor(command => command.ChunkHash)
            .Matches("^[a-fA-F0-9]{64}$")
            .When(command => !string.IsNullOrWhiteSpace(command.ChunkHash))
            .WithMessage("A part checksum must be a 64-character SHA-256 digest.");
    }
}
