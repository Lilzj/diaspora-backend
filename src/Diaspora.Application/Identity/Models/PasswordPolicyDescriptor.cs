namespace Diaspora.Application.Identity.Models;

/// <summary>
/// A machine-readable statement of the active password rules, so clients can show
/// requirements up front instead of guessing from rejection messages.
/// </summary>
public sealed record PasswordPolicyDescriptor
{
    public required int MinimumLength { get; init; }

    public required int MaximumLength { get; init; }

    public required bool RequiresUppercase { get; init; }

    public required bool RequiresLowercase { get; init; }

    public required bool RequiresDigit { get; init; }

    public required bool RequiresNonAlphanumeric { get; init; }

    public required bool ForbidsPersonalInformation { get; init; }

    public required int HistoryDepth { get; init; }
}
