namespace Diaspora.Platform.Operations.Configuration;

/// <summary>
/// What kind of value a setting holds. Used to parse and to validate.
/// <para>
/// The member names avoid <c>String</c>, <c>Integer</c>, <c>Decimal</c> and <c>Boolean</c> because
/// CA1720 refuses identifiers that name a CLR type — a rule the platform builds with as an error.
/// The names chosen say what the operator sees rather than what the runtime stores, which reads
/// better on the administration surface anyway.
/// </para>
/// </summary>
public enum ConfigurationValueKind
{
    Text = 0,
    WholeNumber = 1,
    RealNumber = 2,
    TrueFalse = 3,
    Duration = 4,

    /// <summary>A comma-separated list.</summary>
    List = 5
}

/// <summary>
/// A setting a deployment may change without a restart.
/// <para>
/// <b>Declared, not discovered.</b> A runtime configuration store keyed on arbitrary strings is a
/// store where a typo is a new setting and the real one keeps its old value — silently, and for as
/// long as it takes somebody to notice the change did nothing. Every key here is declared by the
/// module that reads it, with a default, a kind and a validator, and a write to an undeclared key is
/// refused.
/// </para>
/// <para>
/// <b>Not a replacement for <c>IConfiguration</c>.</b> Connection strings, signing keys and anything
/// read once at startup stay where they are: a setting that only takes effect on restart gains
/// nothing from living in a hot-reloadable store, and putting it there implies a capability that
/// does not exist.
/// </para>
/// </summary>
/// <param name="Key">Dotted, lower case, namespaced to the owning module.</param>
/// <param name="Module">Who owns it.</param>
/// <param name="Description">What it does. Shown to whoever is changing it.</param>
/// <param name="Kind">How the value is parsed.</param>
/// <param name="DefaultValue">
/// The compiled-in value. Used when nothing is set, and used as the fallback when a set value fails
/// validation — a bad value must not take the setting out of service.
/// </param>
/// <param name="AllowedValues">When set, the only permitted values.</param>
/// <param name="Minimum">Inclusive lower bound for a numeric or duration setting.</param>
/// <param name="Maximum">Inclusive upper bound.</param>
/// <param name="RequiresRestart">
/// Whether a change takes effect only after a restart. Declaring this is what stops an operator
/// changing a value, seeing no effect and changing it again.
/// </param>
/// <param name="Sensitive">
/// Whether the value must be redacted everywhere it is displayed or logged. A sensitive value is
/// still not a secret — secrets belong in the secret store and never here.
/// </param>
public sealed record ConfigurationDefinition(
    string Key,
    string Module,
    string Description,
    ConfigurationValueKind Kind,
    string DefaultValue,
    IReadOnlyList<string>? AllowedValues = null,
    double? Minimum = null,
    double? Maximum = null,
    bool RequiresRestart = false,
    bool Sensitive = false);

/// <summary>A setting's current state.</summary>
/// <param name="Definition">What it is.</param>
/// <param name="Value">What it is set to, or the default when nothing is set.</param>
/// <param name="IsOverridden">Whether the value differs from the default.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="UpdatedBy">Who last changed it.</param>
public sealed record ConfigurationEntry(
    ConfigurationDefinition Definition,
    string Value,
    bool IsOverridden,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    /// <summary>The value, redacted when the definition says it is sensitive.</summary>
    public string DisplayValue => Definition.Sensitive ? "********" : Value;
}

/// <summary>One change, for the audit trail.</summary>
public sealed record ConfigurationChange(
    string Key,
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    string? Before,
    string? After,
    string? Reason)
{
    /// <summary>
    /// The before and after, redacted when the setting is sensitive.
    /// <para>
    /// The audit trail records that a sensitive value changed and never what it changed to. An audit
    /// log that faithfully preserves every value a sensitive setting has ever held is a second copy of
    /// the thing being protected, in the place people are least careful about.
    /// </para>
    /// </summary>
    public ConfigurationChange Redact() => this with { Before = "********", After = "********" };
}

/// <summary>The result of validating a proposed value.</summary>
/// <param name="IsValid">Whether it may be applied.</param>
/// <param name="Problem">Why not, when it may not.</param>
public readonly record struct ConfigurationValidation(bool IsValid, string? Problem)
{
    public static ConfigurationValidation Valid { get; } = new(true, null);

    public static ConfigurationValidation Invalid(string problem) => new(false, problem);
}

/// <summary>
/// Validates a proposed value beyond what the definition can express.
/// <para>
/// The declaration covers kind, range and allowed values, which is most of it. This is for the rest:
/// a value that must be a multiple of another setting, a cron expression that must parse, a country
/// code that must exist in the catalogue.
/// </para>
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>The key this validates.</summary>
    string Key { get; }

    ConfigurationValidation Validate(string value);
}

/// <summary>Declares a module's runtime settings.</summary>
public interface IRuntimeConfigurationContributor
{
    string Module { get; }

    IEnumerable<ConfigurationDefinition> Settings { get; }
}
