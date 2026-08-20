using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Validation;

/// <summary>
/// Runs every validator once, before the host serves anything, and refuses to start on an error.
/// <para>
/// <b>The whole report, then the decision — in that order and never the other way round.</b> A
/// runner that threw on the first error would tell an operator about one duplicate permission key
/// and hide the other four, so the next deployment finds the second one, and the one after that
/// finds the third. Everything is collected, everything is logged, and then the host stops once.
/// </para>
/// <para>
/// <b>A validator that throws is a finding, not a crash.</b> The point of a boot check is to make
/// the platform's failures legible; a check that takes the process down with a stack trace from
/// inside a reflection call has made things worse than having no check.
/// </para>
/// <para>
/// <b>Registered first among hosted services on purpose.</b> Fail fast means before the outbox
/// dispatcher opens a connection and before five sweeps and a seeder each start their own timer.
/// </para>
/// </summary>
public sealed class PlatformStartupValidationService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ServiceRegistrationSnapshot _registrations;
    private readonly IOptions<PlatformValidationOptions> _options;
    private readonly ILogger<PlatformStartupValidationService> _logger;

    public PlatformStartupValidationService(
        IServiceScopeFactory scopes,
        ServiceRegistrationSnapshot registrations,
        IOptions<PlatformValidationOptions> options,
        ILogger<PlatformStartupValidationService> logger)
    {
        _scopes = scopes;
        _registrations = registrations;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogWarning(
                "Platform startup validation is switched off. The host is starting without checking "
                + "its contributors, permissions, policies, feature gates, seeders or catalogues.");

            return Task.CompletedTask;
        }

        var report = Run(options);

        foreach (var finding in report.Findings)
        {
            if (finding.Severity == StartupFindingSeverity.Error)
            {
                _logger.LogError(
                    "{Code} {Subject}: {Detail}", finding.Code, finding.Subject, finding.Message);
            }
            else
            {
                _logger.LogWarning(
                    "{Code} {Subject}: {Detail}", finding.Code, finding.Subject, finding.Message);
            }
        }

        if (report.ShouldStopTheHost(options.FailOnWarning))
        {
            throw new PlatformValidationException(report);
        }

        _logger.LogInformation(
            "Platform startup validation passed: {Validators} validators, {Warnings} warnings.",
            report.ValidatorCount,
            report.Findings.Count(finding => finding.Severity == StartupFindingSeverity.Warning));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Runs the validators and returns what they found. Public and separate from
    /// <see cref="StartAsync"/> so the architecture and integration suites can assert the real
    /// report rather than assert that a host started, which is a much weaker statement.
    /// </summary>
    public StartupValidationReport Run(PlatformValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var scope = _scopes.CreateScope();

        var context = new StartupValidationContext(
            scope.ServiceProvider,
            _registrations.Descriptors,
            _registrations.Assemblies,
            options);

        var findings = new List<StartupFinding>();
        var validators = scope.ServiceProvider.GetServices<IStartupValidator>().ToList();

        foreach (var validator in validators)
        {
            try
            {
                findings.AddRange(validator.Validate(context));
            }
#pragma warning disable CA1031 // A validator's bug must not be the reason a host cannot start.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _logger.LogError(
                    exception, "The {Validator} startup validator threw.", validator.Name);

                findings.Add(StartupFinding.Warning(
                    "SV0001",
                    validator.Name,
                    "This validator threw and its checks did not run: " +
                    exception.Message.Replace(Environment.NewLine, " ", StringComparison.Ordinal)));
            }
        }

        return new StartupValidationReport(validators.Count, findings);
    }
}

/// <summary>
/// The report, and the one place that decides what a report means.
/// </summary>
public sealed class StartupValidationReport
{
    public StartupValidationReport(int validatorCount, IReadOnlyList<StartupFinding> findings)
    {
        ValidatorCount = validatorCount;

        // Sorted so two boots of the same host produce the same report. A report whose order
        // depends on assembly scan order cannot be diffed between deployments, which is most of
        // what somebody wants a report for.
        Findings = [.. findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ThenBy(finding => finding.Subject, StringComparer.Ordinal)];
    }

    public int ValidatorCount { get; }

    public IReadOnlyList<StartupFinding> Findings { get; }

    public IEnumerable<StartupFinding> Errors =>
        Findings.Where(finding => finding.Severity == StartupFindingSeverity.Error);

    public bool ShouldStopTheHost(bool failOnWarning) =>
        Errors.Any() || (failOnWarning && Findings.Count > 0);

    public string Describe() =>
        string.Join(Environment.NewLine, Findings.Select(finding => "  " + finding));
}

/// <summary>
/// Thrown when the platform refuses to start. Carries the whole report, because the exception
/// message is the only thing an operator is guaranteed to see.
/// </summary>
public sealed class PlatformValidationException : InvalidOperationException
{
    public PlatformValidationException(StartupValidationReport report)
        : base(Describe(report))
    {
        Report = report;
    }

    public PlatformValidationException()
        : base("Platform startup validation failed.")
    {
        Report = new StartupValidationReport(0, []);
    }

    public PlatformValidationException(string message)
        : base(message)
    {
        Report = new StartupValidationReport(0, []);
    }

    public PlatformValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Report = new StartupValidationReport(0, []);
    }

    public StartupValidationReport Report { get; }

    private static string Describe(StartupValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The platform refused to start: {report.Errors.Count()} error(s) and " +
            $"{report.Findings.Count - report.Errors.Count()} warning(s) across " +
            $"{report.ValidatorCount} validators.{Environment.NewLine}{report.Describe()}");
    }
}

/// <summary>
/// A copy of the service collection, taken while it still exists.
/// <para>
/// The container does not keep its descriptors, and the questions this phase needs answered are
/// about descriptors: how many produced this contributor, and is anything at all registered for
/// that seeder. Both are unanswerable from a built provider.
/// </para>
/// </summary>
public sealed class ServiceRegistrationSnapshot
{
    public ServiceRegistrationSnapshot(
        IEnumerable<ServiceDescriptor> descriptors, IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        Descriptors = [.. descriptors];
        Assemblies = assemblies;
    }

    public IReadOnlyList<ServiceDescriptor> Descriptors { get; }

    public IReadOnlyList<Assembly> Assemblies { get; }
}
