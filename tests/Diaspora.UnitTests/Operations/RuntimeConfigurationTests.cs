using Diaspora.Operations.Configuration;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The runtime configuration platform: declaration, the three resolution layers, validation, hot
/// reload and the audit trail.
/// </summary>
public sealed class RuntimeConfigurationServiceTests
{
    [Fact]
    public void An_undeclared_key_reads_as_empty_rather_than_throwing()
    {
        var service = Build();

        service.GetString("nothing.declared").Should().BeEmpty(
            "a configuration read is not a place a request should die");
    }

    [Fact]
    public void A_declared_key_reads_its_default()
    {
        var service = Build(Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        service.GetInt32("test.size").Should().Be(42);
    }

    [Fact]
    public void An_environment_override_beats_the_declared_default()
    {
        var service = Build(
            new Dictionary<string, string?> { ["RuntimeConfiguration:test.size"] = "99" },
            Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        service.GetInt32("test.size").Should().Be(99,
            "a deployment must be able to ship a value without an API call, and that value is the " +
            "one that gets reviewed");
    }

    [Fact]
    public async Task A_runtime_value_beats_an_environment_override()
    {
        var service = Build(
            new Dictionary<string, string?> { ["RuntimeConfiguration:test.size"] = "99" },
            Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        await service.SetAsync("test.size", "7", "operator", "tuning");

        service.GetInt32("test.size").Should().Be(7);
    }

    [Fact]
    public void An_invalid_environment_override_falls_through_to_the_default()
    {
        var service = Build(
            new Dictionary<string, string?> { ["RuntimeConfiguration:test.size"] = "not-a-number" },
            Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        service.GetInt32("test.size").Should().Be(42,
            "a bad value must be loud without being fatal: the platform behaves as declared and the " +
            "failure is counted");
    }

    [Fact]
    public async Task A_value_outside_the_declared_range_is_refused()
    {
        var service = Build(
            Setting("test.size", ConfigurationValueKind.WholeNumber, "42") with
            {
                Minimum = 1,
                Maximum = 100
            });

        var set = async () => await service.SetAsync("test.size", "500", "operator", null);

        await set.Should().ThrowAsync<ArgumentException>().WithMessage("*Above the maximum*");
    }

    [Fact]
    public async Task A_value_outside_the_allowed_set_is_refused()
    {
        var service = Build(
            Setting("test.mode", ConfigurationValueKind.Text, "fast") with
            {
                AllowedValues = ["fast", "thorough"]
            });

        var set = async () => await service.SetAsync("test.mode", "reckless", "operator", null);

        await set.Should().ThrowAsync<ArgumentException>().WithMessage("*is not one of*");
    }

    [Fact]
    public async Task Setting_an_undeclared_key_is_refused()
    {
        var service = Build();

        var set = async () => await service.SetAsync("nothing.declared", "x", "operator", null);

        await set.Should().ThrowAsync<KeyNotFoundException>(
            "a store keyed on arbitrary strings is one where a typo is a new setting and the real " +
            "one silently keeps its old value");
    }

    [Fact]
    public void A_declared_default_that_fails_its_own_declaration_stops_the_boot()
    {
        var build = () => Build(
            Setting("test.size", ConfigurationValueKind.WholeNumber, "not-a-number"));

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared default for 'test.size' is invalid*",
                "the fallback path leads to the default, so an invalid one leaves the setting with " +
                "no valid value at any layer");
    }

    [Fact]
    public async Task A_change_raises_the_hot_reload_event()
    {
        var service = Build(Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        ConfigurationChange? observed = null;
        service.Changed += (_, change) => observed = change;

        await service.SetAsync("test.size", "7", "operator", "tuning");

        observed.Should().NotBeNull();
        observed!.Key.Should().Be("test.size");
        observed.Before.Should().Be("42");
        observed.After.Should().Be("7");
    }

    [Fact]
    public async Task A_subscriber_that_throws_does_not_roll_back_the_change()
    {
        var service = Build(Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        service.Changed += (_, _) => throw new InvalidOperationException("my rebuild failed");

        await service.SetAsync("test.size", "7", "operator", null);

        service.GetInt32("test.size").Should().Be(7,
            "the setting is the source of truth; a subscriber that failed to rebuild its derived " +
            "object is that subscriber's problem to report");
    }

    [Fact]
    public async Task A_reset_returns_the_setting_to_its_declared_default()
    {
        var service = Build(Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        await service.SetAsync("test.size", "7", "operator", null);
        await service.ResetAsync("test.size", "operator");

        service.GetInt32("test.size").Should().Be(42);
    }

    [Fact]
    public async Task A_sensitive_settings_value_is_redacted_everywhere_it_is_shown()
    {
        var service = Build(
            Setting("test.token", ConfigurationValueKind.Text, "default-value") with { Sensitive = true });

        await service.SetAsync("test.token", "the-real-value", "operator", null);

        service.Find("test.token")!.DisplayValue.Should().Be("********");

        var history = await service.HistoryAsync("test.token");

        history[0].Before.Should().Be("********");
        history[0].After.Should().Be("********",
            "an audit log that preserves every value a sensitive setting has held is a second copy " +
            "of the thing being protected, in the place people are least careful about");
    }

    [Fact]
    public void A_custom_validator_runs_after_the_declarations_own_checks()
    {
        var service = Build(
            configuration: null,
            validators: [new EvenNumbersOnly()],
            settings: Setting("test.size", ConfigurationValueKind.WholeNumber, "42"));

        service.Validate("test.size", "7").IsValid.Should().BeFalse();
        service.Validate("test.size", "8").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Two_modules_declaring_one_key_is_refused()
    {
        var build = () => new RuntimeConfigurationService(
            [
                new Contributor("A", Setting("clash", ConfigurationValueKind.Text, "a")),
                new Contributor("B", Setting("clash", ConfigurationValueKind.Text, "b"))
            ],
            [],
            new ConfigurationBuilder().Build(),
            new FakeClock(),
            NullLogger<RuntimeConfigurationService>.Instance);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared by more than one module*");
    }

    private static ConfigurationDefinition Setting(
        string key, ConfigurationValueKind kind, string defaultValue) =>
        new(key, "Test", "A test setting.", kind, defaultValue);

    private static RuntimeConfigurationService Build(params ConfigurationDefinition[] settings) =>
        Build(configuration: null, validators: [], settings);

    private static RuntimeConfigurationService Build(
        Dictionary<string, string?> configuration, params ConfigurationDefinition[] settings) =>
        Build(configuration, [], settings);

    private static RuntimeConfigurationService Build(
        Dictionary<string, string?>? configuration,
        IConfigurationValidator[] validators,
        params ConfigurationDefinition[] settings) =>
        new(
            [new Contributor("Test", settings)],
            validators,
            new ConfigurationBuilder()
                .AddInMemoryCollection(configuration ?? [])
                .Build(),
            new FakeClock(),
            NullLogger<RuntimeConfigurationService>.Instance);

    private sealed class Contributor : IRuntimeConfigurationContributor
    {
        private readonly ConfigurationDefinition[] _settings;

        public Contributor(string module, params ConfigurationDefinition[] settings)
        {
            Module = module;
            _settings = settings;
        }

        public string Module { get; }

        public IEnumerable<ConfigurationDefinition> Settings => _settings;
    }

    private sealed class EvenNumbersOnly : IConfigurationValidator
    {
        public string Key => "test.size";

        public ConfigurationValidation Validate(string value) =>
            int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var number) &&
            number % 2 == 0
                ? ConfigurationValidation.Valid
                : ConfigurationValidation.Invalid("Must be even.");
    }
}
