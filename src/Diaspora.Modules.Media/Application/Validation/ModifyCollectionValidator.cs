using Diaspora.Modules.Media.Application.Collections;
using FluentValidation;

namespace Diaspora.Modules.Media.Application.Validation;

/// <summary>
/// Shape checks for a collection change.
/// <para>
/// The interesting rule is that a reorder must carry identifiers and the other three must
/// carry exactly one. A reorder with no list, or an add with no asset, is a request that has
/// not said what it wants — and the domain would refuse it with an error about membership
/// rather than about the request, which is a worse message.
/// </para>
/// </summary>
public sealed class ModifyCollectionValidator : AbstractValidator<ModifyCollectionCommand>
{
    private static readonly string[] Operations = ["add", "remove", "reorder", "promote"];

    public ModifyCollectionValidator()
    {
        RuleFor(command => command.CollectionId).NotEmpty();

        RuleFor(command => command.Operation)
            .NotEmpty()
            .Must(operation => Operations.Contains(operation.Trim().ToLowerInvariant()))
            .WithMessage("An operation must be add, remove, reorder or promote.");

        RuleFor(command => command.MediaId)
            .NotNull()
            .When(command => !IsReorder(command.Operation))
            .WithMessage("Say which file.");

        RuleFor(command => command.MediaIds)
            .NotEmpty()
            .When(command => IsReorder(command.Operation))
            .WithMessage("A reorder must list every file in the collection.");

        RuleFor(command => command.Caption).MaximumLength(500);
    }

    private static bool IsReorder(string? operation) =>
        string.Equals(operation?.Trim(), "reorder", StringComparison.OrdinalIgnoreCase);
}
