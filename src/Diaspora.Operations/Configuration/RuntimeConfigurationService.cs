using System.Collections.Concurrent;
using System.Globalization;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Configuration;

/// <summary>
/// The runtime configuration platform: declared keys, validated values, hot reload, audit and
/// fallbacks.
/// <para>
/// <b>Three layers, resolved in order.</b> A value set at runtime wins; then an environment override
/// read from <c>IConfiguration</c> under <c>RuntimeConfiguration:{key}</c>, so a deployment can ship a
/// value without an API call; then the module's declared default. A value that fails validation at any
/// layer falls through to the next, and the failure is counted — which is what makes a bad value loud
/// without being fatal.
/// </para>
/// <para>
/// <b>Runtime values are held in memory and lost on restart</b>, exactly as the feature flags are and
/// for the same reason: there is no store yet. The environment layer is what makes that survivable —
/// a change worth keeping is promoted into configuration, which is also the change that gets reviewed.
/// The durable store is a replacement for this class and for nothing else.
/// </para>
/// </summary>
public sealed class RuntimeConfigurationService : IRuntimeConfiguration, IRuntimeConfigurationAdministration
{
    private const string ConfigurationPrefix = "RuntimeConfiguration";
    private const int HistoryLimit = 500;

    private readonly ConcurrentDictionary<string, ConfigurationDefinition> _definitions =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, SetValue> _values = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ConfigurationChange> _history = new();
    // Concrete rather than IReadOnlyDictionary, at CA1859's insistence: this is read on the
    // validation path of every write and the interface dispatch is measurable there.
    private readonly Dictionary<string, IConfigurationValidator> _validators;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ILogger<RuntimeConfigurationService> _logger;

    public RuntimeConfigurationService(
        IEnumerable<IRuntimeConfigurationContributor> contributors,
        IEnumerable<IConfigurationValidator> validators,
        IConfiguration configuration,
        IClock clock,
        ILogger<RuntimeConfigurationService> logger)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _clock = clock;
        _logger = logger;

        _validators = validators
            .GroupBy(validator => validator.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            foreach (var setting in contributor.Settings ?? [])
            {
                if (!_definitions.TryAdd(setting.Key, setting))
                {
                    throw new InvalidOperationException(
                        $"Runtime setting '{setting.Key}' is declared by more than one module — " +
                        $"'{_definitions[setting.Key].Module}' and '{contributor.Module}'.");
                }

                // A default that fails its own declaration is a defect in the declaration, and it
                // must stop the boot: the fallback path leads here, so an invalid default leaves the
                // setting with no valid value at any layer.
                var validation = ValidateAgainst(setting, setting.DefaultValue);

                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"The declared default for '{setting.Key}' is invalid: {validation.Problem}");
                }
            }
        }
    }

    public event EventHandler<ConfigurationChange>? Changed;

    // ─── Reading ─────────────────────────────────────────────────────────────────────────

    public string GetString(string key) => Resolve(key);

    public int GetInt32(string key) =>
        int.TryParse(Resolve(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    public double GetDouble(string key) =>
        double.TryParse(Resolve(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    public bool GetBoolean(string key) =>
        bool.TryParse(Resolve(key), out var value) && value;

    public TimeSpan GetDuration(string key) =>
        TimeSpan.TryParse(Resolve(key), CultureInfo.InvariantCulture, out var value)
            ? value
            : TimeSpan.Zero;

    public IReadOnlyList<string> GetList(string key) =>
        [.. Resolve(key).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public IReadOnlyList<ConfigurationEntry> All =>
        [.. _definitions.Values
            .Select(Entry)
            .OrderBy(entry => entry.Definition.Key, StringComparer.Ordinal)];

    public ConfigurationEntry? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _definitions.TryGetValue(key, out var definition) ? Entry(definition) : null;
    }

    // ─── Administration ──────────────────────────────────────────────────────────────────

    public Task SetAsync(
        string key,
        string value,
        string? changedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (!_definitions.TryGetValue(key, out var definition))
        {
            throw new KeyNotFoundException(
                $"No module declares a runtime setting named '{key}'. A setting has to be declared " +
                "through IRuntimeConfigurationContributor before it can be set.");
        }

        var validation = ValidateAgainst(definition, value);

        if (!validation.IsValid)
        {
            OperationsTelemetry.ConfigurationValidationFailures.Add(1,
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, key));

            throw new ArgumentException(validation.Problem, nameof(value));
        }

        var before = Resolve(key);

        _values[key] = new SetValue(value, _clock.UtcNow, changedBy);

        var change = new ConfigurationChange(key, _clock.UtcNow, changedBy, before, value, reason);

        RecordChange(definition, change);
        Raise(change);

        _logger.LogWarning(
            "Runtime setting {Key} changed by {ChangedBy}{Restart}. Reason: {Reason}",
            key,
            changedBy ?? "the platform",
            definition.RequiresRestart ? " (takes effect after a restart)" : string.Empty,
            reason ?? "(none given)");

        return Task.CompletedTask;
    }

    public Task ResetAsync(string key, string? changedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_definitions.TryGetValue(key, out var definition))
        {
            throw new KeyNotFoundException($"No module declares a runtime setting named '{key}'.");
        }

        var before = Resolve(key);

        _values.TryRemove(key, out _);

        var change = new ConfigurationChange(key, _clock.UtcNow, changedBy, before, Resolve(key), "reset");

        RecordChange(definition, change);
        Raise(change);

        return Task.CompletedTask;
    }

    public ConfigurationValidation Validate(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _definitions.TryGetValue(key, out var definition)
            ? ValidateAgainst(definition, value)
            : ConfigurationValidation.Invalid($"'{key}' is not a declared setting.");
    }

    public Task<IReadOnlyList<ConfigurationChange>> HistoryAsync(
        string? key = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        IReadOnlyList<ConfigurationChange> changes =
        [
            .. _history
                .Where(change => key is null || string.Equals(change.Key, key, StringComparison.Ordinal))
                .OrderByDescending(change => change.ChangedAt)
                .Take(limit)
        ];

        return Task.FromResult(changes);
    }

    // ─── Internals ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a key through the three layers, falling through on anything invalid.
    /// <para>
    /// A validation failure at the runtime or environment layer is counted and logged, then stepped
    /// over. The declared default is validated at construction, so the last layer cannot fail — which
    /// is what makes every getter total.
    /// </para>
    /// </summary>
    private string Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_definitions.TryGetValue(key, out var definition))
        {
            _logger.LogWarning(
                "Runtime setting {Key} was read and is not declared by any module. Returning an empty " +
                "value; a reader should declare the setting it reads.", key);

            return string.Empty;
        }

        if (_values.TryGetValue(key, out var set))
        {
            if (ValidateAgainst(definition, set.Value).IsValid)
            {
                return set.Value;
            }

            _logger.LogError(
                "Runtime setting {Key} holds a value that no longer validates. Falling back.", key);
        }

        var configured = _configuration[$"{ConfigurationPrefix}:{key}"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (ValidateAgainst(definition, configured).IsValid)
            {
                return configured;
            }

            OperationsTelemetry.ConfigurationValidationFailures.Add(1,
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, key));

            _logger.LogError(
                "The environment override for {Key} is invalid and is being ignored. The declared " +
                "default is in force.", key);
        }

        return definition.DefaultValue;
    }

    private ConfigurationValidation ValidateAgainst(ConfigurationDefinition definition, string value)
    {
        if (value is null)
        {
            return ConfigurationValidation.Invalid("A value is required.");
        }

        switch (definition.Kind)
        {
            case ConfigurationValueKind.WholeNumber:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return ConfigurationValidation.Invalid($"'{value}' is not an integer.");
                }

                if (OutOfRange(definition, integer, out var integerProblem))
                {
                    return ConfigurationValidation.Invalid(integerProblem);
                }

                break;

            case ConfigurationValueKind.RealNumber:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return ConfigurationValidation.Invalid($"'{value}' is not a number.");
                }

                if (OutOfRange(definition, number, out var numberProblem))
                {
                    return ConfigurationValidation.Invalid(numberProblem);
                }

                break;

            case ConfigurationValueKind.TrueFalse:
                if (!bool.TryParse(value, out _))
                {
                    return ConfigurationValidation.Invalid($"'{value}' is not true or false.");
                }

                break;

            case ConfigurationValueKind.Duration:
                if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration))
                {
                    return ConfigurationValidation.Invalid($"'{value}' is not a duration (hh:mm:ss).");
                }

                if (OutOfRange(definition, duration.TotalSeconds, out var durationProblem))
                {
                    return ConfigurationValidation.Invalid(durationProblem);
                }

                break;

            case ConfigurationValueKind.List:
            case ConfigurationValueKind.Text:
            default:
                break;
        }

        if (definition.AllowedValues is { Count: > 0 } allowed &&
            !allowed.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)))
        {
            return ConfigurationValidation.Invalid(
                $"'{value}' is not one of: {string.Join(", ", allowed)}.");
        }

        return _validators.TryGetValue(definition.Key, out var validator)
            ? validator.Validate(value)
            : ConfigurationValidation.Valid;
    }

    /// <summary>
    /// Range check. For a duration the bounds are read as seconds, which is stated in the
    /// declaration's documentation because a <c>Minimum</c> of 30 on a duration is otherwise ambiguous.
    /// </summary>
    private static bool OutOfRange(ConfigurationDefinition definition, double value, out string problem)
    {
        if (definition.Minimum is { } minimum && value < minimum)
        {
            problem = $"Below the minimum of {minimum.ToString(CultureInfo.InvariantCulture)}.";
            return true;
        }

        if (definition.Maximum is { } maximum && value > maximum)
        {
            problem = $"Above the maximum of {maximum.ToString(CultureInfo.InvariantCulture)}.";
            return true;
        }

        problem = string.Empty;
        return false;
    }

    private ConfigurationEntry Entry(ConfigurationDefinition definition)
    {
        var value = Resolve(definition.Key);
        var set = _values.GetValueOrDefault(definition.Key);

        return new ConfigurationEntry(
            definition,
            value,
            !string.Equals(value, definition.DefaultValue, StringComparison.Ordinal),
            set?.SetAt,
            set?.SetBy);
    }

    private void RecordChange(ConfigurationDefinition definition, ConfigurationChange change)
    {
        _history.Enqueue(definition.Sensitive ? change.Redact() : change);

        OperationsTelemetry.ConfigurationChanges.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, change.Key),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Module, definition.Module));

        while (_history.Count > HistoryLimit && _history.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// Notifies subscribers, without letting one of them break the change.
    /// <para>
    /// A handler that throws must not roll back a setting that has already been applied, and must not
    /// stop the other handlers hearing about it. The setting is the source of truth; a subscriber that
    /// failed to rebuild its derived object is that subscriber's problem to report.
    /// </para>
    /// </summary>
    private void Raise(ConfigurationChange change)
    {
        foreach (var handler in Changed?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler<ConfigurationChange>)handler)(this, change);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "A subscriber threw while reacting to the change of {Key}. The new value stands.",
                    change.Key);
            }
        }
    }

    private sealed record SetValue(string Value, DateTimeOffset SetAt, string? SetBy);
}
