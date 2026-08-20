namespace Diaspora.Platform.Validation;

/// <summary>
/// How strict the boot-time check is.
/// <para>
/// <b>The defaults are the ones a production host wants and the ones a developer wants are the
/// same.</b> Errors stop the host because an error means the platform would answer requests
/// incorrectly rather than incompletely — a duplicate permission key, a policy no endpoint can
/// satisfy. Warnings are logged and the host starts, because a warning is a drift somebody should
/// fix on their own schedule and not a reason to fail a deployment at three in the morning.
/// </para>
/// <para>
/// <see cref="FailOnWarning"/> exists for the build pipeline, where the schedule is now.
/// </para>
/// </summary>
public sealed class PlatformValidationOptions
{
    public const string SectionName = "PlatformValidation";

    /// <summary>
    /// Whether the check runs at all. Left on. The switch exists so a deployment recovering from an
    /// unrelated incident can start a host it knows is imperfect, not as a way of living with
    /// findings.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether a warning is also grounds for refusing to start.</summary>
    public bool FailOnWarning { get; set; }

    /// <summary>
    /// Whether the reflective catalogue sweep runs. It calls every contributor's declaration method
    /// to look for duplicate keys across module reference data — notification categories and
    /// templates, ranking strategies, recommendation policies, listing categories — which is the
    /// only way the kernel can check them without referencing a single module.
    /// <para>
    /// On by default, and switchable because it is the one validator that executes module code.
    /// Every call is wrapped: a contributor that throws is skipped and logged, never fatal.
    /// </para>
    /// </summary>
    public bool ValidateModuleCatalogs { get; set; } = true;
}
