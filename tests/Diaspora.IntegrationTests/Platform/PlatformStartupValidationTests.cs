using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Validation;
using Diaspora.Platform.Validation.Validators;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The boot-time check, run against the real host.
/// <para>
/// <b>The host starting is the weakest possible statement about validation and it is the one a
/// suite gets for free.</b> Every other test in this project boots the same host, so if validation
/// failed they would all fail — which proves the runner throws, and proves nothing about what the
/// validators looked at. These tests read the report instead: how many validators ran, what they
/// found, and whether the platform is clean or merely unexamined.
/// </para>
/// <para>
/// <b>A clean report is asserted, not hoped for.</b> Three of the findings this suite once produced
/// were the validators being wrong rather than the platform — a composite-keyed catalogue read as
/// eleven duplicates, four platform-level contributors read as missing modules, and two
/// deliberately role-only permissions read as unreachable. Each was a rule overreaching, and each
/// would have been invisible if this file had only asked whether the host came up.
/// </para>
/// </summary>
public sealed class PlatformStartupValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public PlatformStartupValidationTests(
        CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        // The host has to be up before anything can be resolved from it, and the host being up is
        // itself the first assertion: the runner throws on an error finding, so a platform with one
        // never reaches the body of any test below.
        using var client = factory.CreateClient();
    }

    /// <summary>
    /// The whole platform — twenty-two projects, fifteen modules, several hundred permissions,
    /// gates and policies — passes every rule the kernel checks, with nothing to say.
    /// </summary>
    [Fact]
    public void The_platform_validates_cleanly()
    {
        var report = Report();

        foreach (var finding in report.Findings)
        {
            _output.WriteLine(finding.ToString());
        }

        report.Errors.Should().BeEmpty(
            "an error finding means the platform would answer requests incorrectly rather than " +
            "incompletely, and the host refuses to start on one");

        report.Findings.Should().BeEmpty(
            "a warning that fires on a correct platform on every boot is a warning people learn to " +
            "scroll past, which costs more than not having it");
    }

    /// <summary>
    /// Every validator the kernel ships actually ran. A report with no findings is equally
    /// consistent with a clean platform and with a runner that found no validators.
    /// <para>
    /// <b>At least, not exactly.</b> This asserted equality until the Communication Platform shipped
    /// the first module-owned validator, which is the seam working as B1.4 intended — validators are
    /// discovered across every platform assembly precisely so a module can add one. The property
    /// worth keeping is that the kernel's own six ran, which
    /// <see cref="The_kernel_validators_are_the_ones_the_phase_shipped"/> asserts by name; equality
    /// would additionally assert that no module ever contributes, which was never the rule.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_kernel_validator_ran()
    {
        Report().ValidatorCount.Should().BeGreaterThanOrEqualTo(
            PlatformStartupValidationRegistration.KernelValidators.Count,
            "the six the kernel ships are discovered the same way a module's would be, so a " +
            "discovery that silently found five would report a clean platform it never looked at");
    }

    /// <summary>
    /// The validators are found by scanning, which means a module could add one — and means a
    /// typo in a namespace would remove one just as silently. Each is named.
    /// </summary>
    [Fact]
    public void The_kernel_validators_are_the_ones_the_phase_shipped()
    {
        using var scope = _factory.Services.CreateScope();

        var running = scope.ServiceProvider.GetServices<IStartupValidator>()
            .Select(validator => validator.GetType())
            .ToList();

        running.Should().Contain(
        [
            typeof(ModuleMetadataValidator),
            typeof(ContributorRegistrationValidator),
            typeof(AuthorizationCatalogValidator),
            typeof(FeatureGateValidator),
            typeof(SeederRegistrationValidator),
            typeof(ModuleCatalogValidator)
        ]);
    }

    /// <summary>
    /// The reflective catalogue sweep can be switched off, and switching it off must not make the
    /// rest of the report disappear with it — the option is a scalpel, not a mute button.
    /// </summary>
    [Fact]
    public void The_catalogue_sweep_can_be_switched_off_without_silencing_the_rest()
    {
        var service = _factory.Services.GetRequiredService<PlatformStartupValidationService>();

        var report = service.Run(
            new PlatformValidationOptions { ValidateModuleCatalogs = false });

        report.ValidatorCount.Should().BeGreaterThanOrEqualTo(
            PlatformStartupValidationRegistration.KernelValidators.Count);
        report.Errors.Should().BeEmpty();
    }

    private StartupValidationReport Report() =>
        _factory.Services
            .GetRequiredService<PlatformStartupValidationService>()
            .Run(new PlatformValidationOptions());
}
