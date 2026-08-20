namespace Diaspora.Platform.Services.Models;

public sealed record EligibilityResult
{
    public EligibilityStatus Status { get; init; }
    public IReadOnlyList<string> RequiredActions { get; init; } = [];
    public IReadOnlyList<string> CurrentRestrictions { get; init; } = [];
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];

    public static EligibilityResult Eligible() => new() { Status = EligibilityStatus.Eligible };

    public static EligibilityResult NotEligible(
        IReadOnlyList<string>? requiredActions = null,
        IReadOnlyList<string>? restrictions = null,
        IReadOnlyList<string>? reasonCodes = null) => new()
    {
        Status = EligibilityStatus.NotEligible,
        RequiredActions = requiredActions ?? [],
        CurrentRestrictions = restrictions ?? [],
        ReasonCodes = reasonCodes ?? []
    };
}
